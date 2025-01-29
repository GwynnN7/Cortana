using System.Net.Sockets;
using System.Text.Json;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.DataStructures;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.SocketHandler;

internal class SensorsHandler : ClientHandler
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();

	private Timer? _motionTimer;
	private SensorData? _lastSensorData;

	internal SensorsHandler(Socket socket) : base(socket, "ESP32") { }

	protected override void HandleRead(string message)
	{
		var newData = JsonSerializer.Deserialize<SensorData>(message);

		if (_lastSensorData != null)
		{
			switch (newData)
			{
				case { WideMotion: EPower.On } when _lastSensorData is {WideMotion: EPower.Off}:
				case { PreciseMotion: EPower.On } when _lastSensorData is {PreciseMotion: EPower.Off}:
					FileHandler.Log("SensorsLog", message);
					break;
			}
		}
		
		if (Service.CurrentControlMode == EControlMode.Automatic)
		{
			if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPower.On)
			{
				switch (newData)
				{
					case { PreciseMotion: EPower.Off, WideMotion: EPower.Off } when _motionTimer == null:
					{
						int seconds = HardwareApi.Devices.GetPower(EDevice.Computer) == EPower.On ? 60 : 15;
						_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
						_motionTimer.Set((seconds, 0, 0));
						break;
					}
					case { PreciseMotion: EPower.On } or { WideMotion: EPower.On }:
						_motionTimer?.Destroy();
						_motionTimer = null;
						break;
				}
			}
			else
			{
				switch (newData)
				{
					case { PreciseMotion: EPower.On } or { WideMotion: EPower.On }:
					{
						if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EPower.Off && newData.Light <= Service.Settings.LightThreshold)
						{
							HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.On);
							HardwareNotifier.Publish("Motion detected, switching lamp on!", ENotificationPriority.High);
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
		HardwareNotifier.Publish("No motion detected, switching lamp off...", ENotificationPriority.Low);

		return Task.CompletedTask;
	}
	
	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();
		lock (InstanceLock) _instance = null;
	}
	
	// Static methods
	
	internal static int? GetRoomLightLevel()
	{
		lock (InstanceLock) 
			return _instance?._lastSensorData?.Light;
	}
	
	internal static double? GetRoomTemperature()
	{
		lock (InstanceLock)
			return _instance?._lastSensorData?.Temperature;
	}
	
	internal static EPower? GetMotionDetected()
	{
		lock (InstanceLock)
		{
			if(_instance?._lastSensorData == null) return null;
			return _instance._lastSensorData.Value.WideMotion == EPower.On || _instance._lastSensorData.Value.PreciseMotion == EPower.On ? EPower.On : EPower.Off;
		}
	}
	
	internal static void BindNew(SensorsHandler sensorHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = sensorHandler;
		}
	}
    
	internal static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = null;
		}
	}
}