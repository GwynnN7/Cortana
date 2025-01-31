using System.Net.Sockets;
using System.Text.Json;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using Utility;
using Utility.Structures;
using Timer = Utility.Timer;

namespace CortanaKernel.Hardware.SocketHandler;

public class SensorsHandler : ClientHandler
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();

	private Timer? _motionTimer;
	private SensorData? _lastSensorData;

	public SensorsHandler(Socket socket) : base(socket, "ESP32") { }

	protected override void HandleRead(string message)
	{
		var newData = JsonSerializer.Deserialize<SensorData>(message, FileHandler.SerializerOptions);

		if (_lastSensorData != null)
		{
			switch (newData)
			{
				case { WideMotion: EPowerStatus.On } when _lastSensorData is {WideMotion: EPowerStatus.Off}:
				case { PreciseMotion: EPowerStatus.On } when _lastSensorData is {PreciseMotion: EPowerStatus.Off}:
					FileHandler.Log("SensorsLog", message);
					break;
			}
		}
		
		if (Service.CurrentControlMode == EControlMode.Automatic)
		{
			if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.On)
			{
				switch (newData)
				{
					case { PreciseMotion: EPowerStatus.Off, WideMotion: EPowerStatus.Off } when _motionTimer == null:
					{
						int seconds = HardwareApi.Devices.GetPower(EDevice.Computer) == EPowerStatus.On ? 60 : 15;
						_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
						_motionTimer.Set((seconds, 0, 0));
						break;
					}
					case { PreciseMotion: EPowerStatus.On } or { WideMotion: EPowerStatus.On }:
						_motionTimer?.Destroy();
						_motionTimer = null;
						break;
				}
			}
			else
			{
				switch (newData)
				{
					case { PreciseMotion: EPowerStatus.On } or { WideMotion: EPowerStatus.On }:
					{
						if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.Off && newData.Light <= Service.Settings.LightThreshold)
						{
							HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.On);
							HardwareNotifier.Publish("Motion detected, switching lamp on!");
						}
						
						break;
					}
				}
			}
		}

		lock (InstanceLock) _lastSensorData = newData;
	}

	private Task MotionTimeout(object? sender) 
	{
		_motionTimer?.Destroy();
		_motionTimer = null;
		HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Off);
		HardwareNotifier.Publish("No motion detected, switching lamp off...");

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
			return _instance._lastSensorData.Value.WideMotion == EPowerStatus.On || _instance._lastSensorData.Value.PreciseMotion == EPowerStatus.On ? EPowerStatus.On : EPowerStatus.Off;
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