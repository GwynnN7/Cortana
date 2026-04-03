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

	private Timer? _motionTimer = null;
	private Timer? _airQualityTimer = null;
	private bool _airQualityWarningSent = false;
	private SensorData? _lastSensorData;

	protected override void HandleRead(string message)
	{
		var newData = JsonSerializer.Deserialize<SensorData>(message, DataHandler.SerializerOptions);

		if (Service.Settings.AutomaticMode == EStatus.On)
		{
			if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EStatus.On)
			{
				switch (newData)
				{
					case { Motion: (int)EStatus.Off } when _motionTimer == null:
						{
							int seconds = HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.On
								? Service.Settings.MotionOffMax
								: Service.Settings.MotionOffMin;
							_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
							_motionTimer.Set((seconds, 0, 0));
							break;
						}
					case { Motion: (int)EStatus.On }:
						_motionTimer?.Destroy();
						_motionTimer = null;
						break;
				}
			}
			else if (newData.Motion == (int)EStatus.On)
			{
				if (HardwareApi.Devices.GetPower(EDevice.Lamp) == EStatus.Off && newData.Light <= Service.Settings.LightThreshold)
				{
					HardwareApi.Devices.Switch(EDevice.Lamp, ESwitchAction.On, automatic: true);
					IpcService.Publish(EMessageCategory.Telegram, "Motion detected, switching lamp on");
				}
			}
		}

		if (newData.Motion == (int)EStatus.On)
		{
			if (newData.Tvoc >= Service.Settings.TvocThreshold || newData.Eco2 >= Service.Settings.Eco2Threshold)
			{
				if (!_airQualityWarningSent)
				{
					IpcService.Publish(EMessageCategory.Telegram, "Air quality warning! You should open the window");

					_airQualityTimer = new Timer("air-quality-timer", null, async sender =>
					{
						_airQualityWarningSent = false;
						_airQualityTimer?.Destroy();
					}, ETimerType.Utility).Set((0, 30, 0));

					_airQualityWarningSent = true;
				}
			}
			else
			{
				if (_airQualityWarningSent)
				{
					_airQualityWarningSent = false;
					_airQualityTimer?.Destroy();
					IpcService.Publish(EMessageCategory.Telegram, "Air quality back to normal");
				}
			}
		}

		lock (InstanceLock) _lastSensorData = newData;
	}

	private Task MotionTimeout(object? sender)
	{
		_motionTimer?.Destroy();
		_motionTimer = null;
		if (Service.Settings.AutomaticMode == EStatus.Off) return Task.CompletedTask;

		HardwareApi.Devices.Switch(EDevice.Lamp, ESwitchAction.Off, automatic: true);
		IpcService.Publish(EMessageCategory.Telegram, "No motion detected, switching lamp off");

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

	public static double? GetRoomHumidity()
	{
		lock (InstanceLock)
			return _instance?._lastSensorData?.Humidity;
	}

	public static int? GetRoomEco2()
	{
		lock (InstanceLock)
			return _instance?._lastSensorData?.Eco2;
	}

	public static int? GetRoomTvoc()
	{
		lock (InstanceLock)
			return _instance?._lastSensorData?.Tvoc;
	}

	public static EStatus? GetMotionDetected()
	{
		lock (InstanceLock)
		{
			if (_instance?._lastSensorData == null) return null;
			return _instance._lastSensorData.Value.Motion == (int)EStatus.On ? EStatus.On : EStatus.Off;
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