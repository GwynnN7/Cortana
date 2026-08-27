using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaKernel.Hardware.SocketHandler;

public class SensorsHandler : ClientHandler
{
	private static readonly Lock InstanceLock = new();
	private static SensorsHandler? _instance;

	private static readonly TimeSpan AirQualityWarningCooldown = TimeSpan.FromMinutes(45);
	private const int MaxBufferLength = 8192;

	private readonly Lock _stateLock = new();
	private readonly StringBuilder _receiveBuffer = new();

	private Timer? _motionTimer;
	private Timer? _airQualityTimer;
	private bool _airQualityWarningSent;
	private SensorData? _lastSensorData;
	private DateTime _lastUpdate = DateTime.MinValue;

	public SensorsHandler(Socket socket, string? pendingData = null) : base(socket, "ESP32", pendingData) { }

	protected override void HandleRead(string message)
	{
		foreach (SensorData data in ExtractFrames(message)) ProcessSensorData(data);
	}

	private IEnumerable<SensorData> ExtractFrames(string chunk)
	{
		var frames = new List<SensorData>();

		lock (_stateLock)
		{
			_receiveBuffer.Append(chunk);
			string buffer = _receiveBuffer.ToString();

			var depth = 0;
			var start = -1;
			var consumed = 0;

			for (var i = 0; i < buffer.Length; i++)
			{
				switch (buffer[i])
				{
					case '{':
						if (depth++ == 0) start = i;
						break;
					case '}':
						if (depth == 0 || --depth != 0) break;

						string json = buffer[start..(i + 1)];
						consumed = i + 1;
						try
						{
							frames.Add(JsonSerializer.Deserialize<SensorData>(json, DataHandler.SerializerOptions));
						}
						catch (JsonException ex)
						{
							DataHandler.Log($"[ESP32] Dropping malformed frame: {ex.Message}");
						}
						break;
				}
			}

			_receiveBuffer.Remove(0, consumed);
			if (_receiveBuffer.Length > MaxBufferLength) _receiveBuffer.Clear();
		}

		return frames;
	}

	private void ProcessSensorData(SensorData newData)
	{
		HandleAutomaticLighting(newData);
		HandleAirQuality(newData);

		lock (_stateLock)
		{
			_lastSensorData = newData;
			_lastUpdate = DateTime.UtcNow;
		}

		SystemEvents.Notify();
	}

		private void HandleAutomaticLighting(SensorData newData)
	{
		bool lampOn = HardwareApi.Devices.GetPower(EDevice.Lamp) == EStatus.On;
		bool motion = newData.Motion == (int)EStatus.On;

		if (lampOn)
		{
			if (motion)
			{
				CancelMotionTimer();
				return;
			}

			if (!AutomationService.CanAutoExtinguish) return;
			ArmMotionTimer(AutomationService.MotionOffSeconds);
			return;
		}

		CancelMotionTimer();

		if (!motion || !AutomationService.CanAutoLight) return;
		if (newData.Light > AutomationService.Settings.LightThreshold) return;

		HardwareApi.Devices.Switch(EDevice.Lamp, ESwitchAction.On, automatic: true);

		if (HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.Off)
			Notifier.Send(ELogSource.Motion, "Motion detected, switching lamp on");
	}

	private void ArmMotionTimer(int seconds)
	{
		lock (_stateLock)
		{
			if (_motionTimer != null) return;
			_motionTimer = new Timer("motion-timer", null, MotionTimeout, ETimerType.Utility);
			_motionTimer.Set((seconds, 0, 0));
		}
	}

	private void CancelMotionTimer()
	{
		lock (_stateLock)
		{
			_motionTimer?.Destroy();
			_motionTimer = null;
		}
	}

	private void HandleAirQuality(SensorData newData)
	{
		if (newData.Motion != (int)EStatus.On) return;

		bool overThreshold = newData.Tvoc >= AutomationService.Settings.TvocThreshold * 1.15 || newData.Eco2 >= AutomationService.Settings.Eco2Threshold * 1.15;
		bool backToNormal = newData.Tvoc < AutomationService.Settings.TvocThreshold * 0.9 && newData.Eco2 < AutomationService.Settings.Eco2Threshold * 0.9;

		lock (_stateLock)
		{
			if (overThreshold && !_airQualityWarningSent)
			{
				Notifier.Send(ELogSource.AirQuality, "Air quality warning, you should open the window", ELogLevel.Alert);
				_airQualityWarningSent = true;

				_airQualityTimer?.Destroy();
				_airQualityTimer = new Timer("air-quality-timer", null, ClearAirQualityWarning, ETimerType.Utility)
					.Set((0, (int)AirQualityWarningCooldown.TotalMinutes, 0));
			}
			else if (backToNormal && _airQualityWarningSent)
			{
				_airQualityWarningSent = false;
				_airQualityTimer?.Destroy();
				_airQualityTimer = null;
				Notifier.Send(ELogSource.AirQuality, "Air quality back to normal");
			}
		}
	}

	private Task ClearAirQualityWarning(object? sender)
	{
		lock (_stateLock)
		{
			_airQualityWarningSent = false;
			_airQualityTimer = null;
		}
		return Task.CompletedTask;
	}

	private Task MotionTimeout(object? sender)
	{
		lock (_stateLock) _motionTimer = null;

		if (!AutomationService.CanAutoExtinguish) return Task.CompletedTask;

		HardwareApi.Devices.Switch(EDevice.Lamp, ESwitchAction.Off, automatic: true);
		if (HardwareApi.Devices.GetPower(EDevice.Computer) == EStatus.Off)
			Notifier.Send(ELogSource.Motion, "No motion detected, switching lamp off");

		return Task.CompletedTask;
	}

	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();

		lock (_stateLock)
		{
			_motionTimer?.Destroy();
			_motionTimer = null;
			_airQualityTimer?.Destroy();
			_airQualityTimer = null;
		}

		lock (InstanceLock)
		{
			if (ReferenceEquals(_instance, this)) _instance = null;
		}
	}

	private static SensorData? Snapshot()
	{
		SensorsHandler? instance;
		lock (InstanceLock) instance = _instance;
		if (instance == null) return null;

		lock (instance._stateLock) return instance._lastSensorData;
	}

	public static bool IsOnline
	{
		get
		{
			SensorsHandler? instance;
			lock (InstanceLock) instance = _instance;
			if (instance == null) return false;

			lock (instance._stateLock) return instance._lastUpdate != DateTime.MinValue;
		}
	}

	public static DateTime? LastUpdate
	{
		get
		{
			SensorsHandler? instance;
			lock (InstanceLock) instance = _instance;
			if (instance == null) return null;

			lock (instance._stateLock) return instance._lastUpdate == DateTime.MinValue ? null : instance._lastUpdate;
		}
	}

	public static int? GetRoomLightLevel() => Snapshot()?.Light;
	public static double? GetRoomTemperature() => Snapshot()?.Temperature;
	public static double? GetRoomHumidity() => Snapshot()?.Humidity;
	public static int? GetRoomEco2() => Snapshot()?.Eco2;
	public static int? GetRoomTvoc() => Snapshot()?.Tvoc;

	public static EStatus? GetMotionDetected()
	{
		SensorData? data = Snapshot();
		if (data == null) return null;
		return data.Value.Motion == (int)EStatus.On ? EStatus.On : EStatus.Off;
	}

	public static void BindNew(SensorsHandler sensorHandler)
	{
		SensorsHandler? previous;
		lock (InstanceLock)
		{
			previous = _instance;
			_instance = sensorHandler;
		}
		previous?.DisconnectIfAvailable();
	}

	public static void Interrupt()
	{
		SensorsHandler? previous;
		lock (InstanceLock)
		{
			previous = _instance;
			_instance = null;
		}
		previous?.DisconnectIfAvailable();
	}
}
