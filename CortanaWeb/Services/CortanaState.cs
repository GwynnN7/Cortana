using CortanaLib;
using CortanaLib.Structures;

namespace CortanaWeb.Services;

public sealed class CortanaState : BackgroundService
{
		private static readonly TimeSpan FallbackPollInterval = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

	private readonly ILogger<CortanaState> _logger;
	private readonly Lock _subscriberLock = new();
	private int _subscribers;

	public CortanaState(ILogger<CortanaState> logger) => _logger = logger;

	private event Action? Changed;

	public SystemStatusResponse? Snapshot { get; private set; }
	public bool Online { get; private set; }
	public DateTimeOffset? LastUpdate { get; private set; }

	public string DeviceStatus(EDevice device) =>
		Snapshot?.Devices.FirstOrDefault(d => d.Device == device.ToString())?.Status ?? "-";

	public bool IsDeviceOn(EDevice device) => DeviceStatus(device) == nameof(EStatus.On);

	public SensorResponse? Sensor(ESensor sensor) => Snapshot?.Sensors.FirstOrDefault(s => s.Sensor == sensor.ToString());

	public string SensorValue(ESensor sensor)
	{
		SensorResponse? reading = Sensor(sensor);
		if (reading == null || string.IsNullOrEmpty(reading.Value)) return "-";
		if (sensor != ESensor.Motion) return $"{reading.Value}{reading.Unit}";
		return reading.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "Detected" : "Clear";
	}

	public bool MotionDetected() => Sensor(ESensor.Motion)?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

		public bool SensorsOnline => Snapshot?.Sensors.Any(s => !string.IsNullOrEmpty(s.Value)) ?? false;

	public string SettingValue(ESettings setting) =>
		Snapshot?.Settings.FirstOrDefault(s => s.Setting == setting.ToString())?.Value ?? "-";

	public int SettingNumber(ESettings setting) =>
		int.TryParse(SettingValue(setting), out int value) ? value : 0;

	public bool SettingEnabled(ESettings setting) => SettingValue(setting) == nameof(EStatus.On);

	public string RaspberryInfo(ERaspberryInfo info)
	{
		SensorResponse? entry = Snapshot?.Raspberry.FirstOrDefault(r => r.Sensor == info.ToString());
		if (entry == null || string.IsNullOrEmpty(entry.Value)) return "-";
		return $"{entry.Value}{entry.Unit}";
	}

	public bool SubfunctionRunning(ESubFunctionType type) =>
		Snapshot?.Subfunctions.FirstOrDefault(s => s.Subfunction == type.ToString())?.Running ?? false;

	public Task<string> SwitchDevice(EDevice device, ESwitchAction action) =>
		Act($"{ERoute.Devices}/{device}", new PostAction(action.ToString()));

	public Task<string> SwitchRoom(ESwitchAction action) =>
		Act($"{ERoute.Devices}/room", new PostAction(action.ToString()));

	public Task<string> Sleep() => Act($"{ERoute.Devices}/sleep");

	public Task<string> ComputerCommand(EComputerCommand command, string args = "") =>
		Act($"{ERoute.Computer}", new PostCommand(command.ToString(), args));

	public Task<string> RaspberryCommand(ERaspberryCommand command, string args = "") =>
		Act($"{ERoute.Raspberry}", new PostCommand(command.ToString(), args));

	public Task<string> SetSetting(ESettings setting, int value) =>
		Act($"{ERoute.Settings}/{setting}", new PostValue(value));

	public Task<string> Subfunction(ESubFunctionType type, ESubfunctionAction action) =>
		Act($"{ERoute.SubFunctions}/{type}", new PostAction(action.ToString()));

	public Task<string> Broadcast(EMessageCategory category, string message) =>
		Act($"{ERoute.SubFunctions}", new PostCommand(category.ToString(), message));

	public async Task<IReadOnlyList<LogEntry>> GetLogs(int limit = 200)
	{
		IOption<LogListResponse> list = await ApiHandler.Get<LogListResponse>($"{ERoute.Logs}?limit={limit}");
		return list.Match(value => value.Entries, () => []);
	}

	public Task<string> ClearLogs() => ApiHandler.Delete($"{ERoute.Logs}");

	public async Task<IReadOnlyList<ScheduleResponse>> GetSchedules()
	{
		IOption<ScheduleListResponse> list = await ApiHandler.Get<ScheduleListResponse>($"{ERoute.Schedules}");
		return list.Match(value => value.Schedules, () => []);
	}

	public Task<string> CreateSchedule(PostSchedule schedule) =>
		ApiHandler.Post($"{ERoute.Schedules}", schedule);

	public Task<string> UpdateSchedule(string id, string command) =>
		ApiHandler.Post($"{ERoute.Schedules}/{id}", new PostScheduleUpdate(command));

	public Task<string> DeleteSchedule(string id) =>
		ApiHandler.Delete($"{ERoute.Schedules}/{id}");

	private async Task<string> Act(string route, object? body = null)
	{
		string result = await ApiHandler.Post(route, body);
		await RefreshAsync();
		return result;
	}

	public IDisposable Subscribe(Action onChanged)
	{
		Changed += onChanged;
		lock (_subscriberLock) _subscribers++;
		return new Subscription(this, onChanged);
	}

	private void Unsubscribe(Action onChanged)
	{
		Changed -= onChanged;
		lock (_subscriberLock) _subscribers = Math.Max(0, _subscribers - 1);
	}

	public int ViewerCount
	{
		get { lock (_subscriberLock) return _subscribers; }
	}

	public async Task RefreshAsync(CancellationToken token = default)
	{
		IOption<SystemStatusResponse> status = await ApiHandler.Get<SystemStatusResponse>("status", token);

		status.Match<object?>(
			snapshot =>
			{
				Apply(snapshot);
				return null;
			},
			() =>
			{
				Online = false;
				NotifySubscribers();
				return null;
			});
	}

	private void Apply(SystemStatusResponse snapshot)
	{
		Snapshot = snapshot;
		Online = true;
		LastUpdate = DateTimeOffset.Now;
		NotifySubscribers();
	}

	private void NotifySubscribers()
	{
		foreach (Action handler in Changed?.GetInvocationList().Cast<Action>() ?? [])
		{
			try
			{
				handler();
			}
			catch (Exception ex)
			{
				_logger.LogDebug("Subscriber notification failed: {Message}", ex.Message);
			}
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ConsumeEventStream(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				_logger.LogInformation("Event stream unavailable ({Message}), falling back to polling", ex.Message);
			}

			if (!await PollUntilStreamRetry(stoppingToken)) return;
		}
	}

	private async Task ConsumeEventStream(CancellationToken token)
	{
		await foreach (SystemStatusResponse snapshot in ApiHandler.Stream<SystemStatusResponse>("events", token))
		{
			Apply(snapshot);
		}
	}

		private async Task<bool> PollUntilStreamRetry(CancellationToken token)
	{
		DateTime retryAt = DateTime.UtcNow.Add(ReconnectDelay);

		while (DateTime.UtcNow < retryAt)
		{
			try
			{
				await RefreshAsync(token);
				await Task.Delay(FallbackPollInterval, token);
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Status refresh failed: {Message}", ex.Message);
			}
		}

		return true;
	}

	private sealed class Subscription(CortanaState state, Action handler) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			state.Unsubscribe(handler);
		}
	}
}
