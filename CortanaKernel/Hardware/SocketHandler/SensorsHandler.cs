using System.Net.Sockets;
using System.Text.Json;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaKernel.Hardware.SocketHandler;

public class SensorsHandler(Socket socket) : ClientHandler(socket, "ESP32")
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();

	private Timer? _motionTimer;
	private SensorData? _lastSensorData;

	protected override void HandleRead(string message)
	{
		var newData = JsonSerializer.Deserialize<SensorData>(message, DataHandler.SerializerOptions);
		
		if (Service.Settings.MotionDetection == EMotionDetection.On)
		{
			if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.On)
			{
				switch (newData)
				{
					case { Motion: (int) EPowerStatus.Off } when _motionTimer == null:
					{
						int seconds = HardwareApi.Devices.GetPower(EDevice.Computer) == EPowerStatus.On
							? Service.Settings.MotionOffMax
							: Service.Settings.MotionOffMin;
						_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
						_motionTimer.Set((seconds, 0, 0));
						break;
					}
					case { Motion: (int) EPowerStatus.On }:
						_motionTimer?.Destroy();
						_motionTimer = null;
						break;
				}
			}
			else if(newData.Motion == (int) EPowerStatus.On)
			{
				if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.Off && newData.Light <= Service.Settings.LightThreshold)
				{
					HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.On);
					IpcService.Publish(EMessageCategory.Update,"Motion detected, switching lamp on!");
				}
			}
		}

		lock (InstanceLock) _lastSensorData = newData;
	}

	private Task MotionTimeout(object? sender) 
	{
		_motionTimer?.Destroy();
		_motionTimer = null;
		if (Service.Settings.MotionDetection == EMotionDetection.Off) return Task.CompletedTask;
		
		HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Off);
		IpcService.Publish(EMessageCategory.Update,"No motion detected, switching lamp off...");

		return Task.CompletedTask;
	}
	
	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();
		lock (InstanceLock) _instance = null;
	}
	
	// Static methods
	
	public static int? GetRoomLightLevel()
	{
		lock (InstanceLock) 
			return _instance?._lastSensorData?.Light;
	}
	
	public static double? GetRoomTemperature()
	{
		lock (InstanceLock)
			return _instance?._lastSensorData?.Temperature;
	}
	
	public static EPowerStatus? GetMotionDetected()
	{
		lock (InstanceLock)
		{
			if(_instance?._lastSensorData == null) return null;
			return _instance._lastSensorData.Value.Motion == (int) EPowerStatus.On ? EPowerStatus.On : EPowerStatus.Off;
		}
	}
	
	public static void BindNew(SensorsHandler sensorHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = sensorHandler;
		}
	}
    
	public static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = null;
		}
	}
}