using System.Net.Sockets;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.Utility;
using Newtonsoft.Json;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.ClientHandlers;

internal class SensorsHandler : ClientHandler
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();

	private Timer? _motionTimer;
	private SensorData? _lastSensorData;

	internal SensorsHandler(Socket socket) : base(socket, "ESP32") { }

	protected override void HandleRead(string message)
	{
		var newData = JsonConvert.DeserializeObject<SensorData>(message);

		if (_lastSensorData != null)
		{
			switch (newData)
			{
				case { BigMotion: EPower.On } when _lastSensorData is {BigMotion: EPower.Off}:
				case { SmallMotion: EPower.On } when _lastSensorData is {SmallMotion: EPower.Off}:
					FileHandler.Log("SensorsLog", message);
					break;
			}
		}
		
		if (HardwareSettings.CurrentControlMode == EControlMode.MotionSensor)
		{
			if (HardwareProxy.GetDevicePower(EDevice.Lamp) == EPower.On)
			{
				switch (newData)
				{
					case { SmallMotion: EPower.Off, BigMotion: EPower.Off } when _motionTimer == null:
					{
						int seconds = HardwareProxy.GetDevicePower(EDevice.Computer) == EPower.On ? 60 : 10;
						_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
						_motionTimer.Set((seconds, 0, 0));
						break;
					}
					case { SmallMotion: EPower.On } or { BigMotion: EPower.On }:
						_motionTimer?.Destroy();
						_motionTimer = null;
						break;
				}
			}
			else
			{
				switch (newData)
				{
					case { SmallMotion: EPower.On } /*or { BigMotion: EPower.On }*/:
					{
						if (HardwareProxy.GetDevicePower(EDevice.Lamp) == EPower.Off)
						{
							HardwareProxy.SwitchDevice(EDevice.Lamp, EPowerAction.On);
							HardwareNotifier.Publish("Motion detected, switching lamp on!", ENotificationPriority.High);
						}
						
						break;
					}
				}
			}
		}

		lock (InstanceLock)
		{
			_lastSensorData = newData;
		}
	}

	private Task MotionTimeout(object? sender) 
	{
		_motionTimer?.Destroy();
		_motionTimer = null;
		HardwareProxy.SwitchDevice(EDevice.Lamp, EPowerAction.Off);
		HardwareNotifier.Publish("No motion detected, switching lamp off...", ENotificationPriority.Low);

		return Task.CompletedTask;
	}
	
	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();
		_instance = null;
	}
	
	// Static methods
	
	internal static int? GetRoomLightLevel()
	{
		lock (InstanceLock)
		{
			return _instance?._lastSensorData?.Light;
		}
	}
	
	internal static float? GetRoomTemperature()
	{
		lock (InstanceLock)
		{
			return _instance?._lastSensorData?.Temperature;
		}
	}
	
	internal static EPower? GetMotionDetected()
	{
		lock (InstanceLock)
		{
			if(_instance?._lastSensorData == null) return null;
			return _instance._lastSensorData.Value.BigMotion == EPower.On || _instance._lastSensorData.Value.SmallMotion == EPower.On ? EPower.On : EPower.Off;
		}
	}
	
	internal static void BindNew(SensorsHandler sensorHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectSocket();
			_instance = sensorHandler;
		}
	}
    
	internal static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectSocket();
			_instance = null;
		}
	}
}