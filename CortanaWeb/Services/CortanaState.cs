using CortanaLib.Client;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaWeb.Services;

/// The dashboard's view of the Kernel
public sealed class CortanaState(ILogger<CortanaState> logger) : BackgroundService
{
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
	private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

	private readonly CortanaClient _client = CortanaClient.Default.As(CommandSurface.Web);

	private event Action? Changed;

	public CortanaSnapshot? Snapshot { get; private set; }

	public bool Online { get; private set; }

	public DateTimeOffset? LastUpdate { get; private set; }

	// ---------- projections ----------

	public DeviceView? Device(DeviceId device) => Snapshot?.Devices.FirstOrDefault(view => view.Device == device);

	public string DeviceStatus(DeviceId device) => Device(device)?.State.ToString() ?? "-";

	public bool IsDeviceOn(DeviceId device) => Device(device)?.State == PowerState.On;

	public SensorView? Sensor(SensorId sensor) => Snapshot?.Sensors.FirstOrDefault(view => view.Sensor == sensor);

	public string SensorValue(SensorId sensor)
	{
		SensorView? reading = Sensor(sensor);
		if (reading is not { Available: true } || reading.Value.Length == 0) return "-";

		return sensor == SensorId.Motion
			? reading.Value == "true" ? "Detected" : "Clear"
			: $"{reading.Value}{reading.Unit}";
	}

	public bool SensorsOnline => Snapshot?.Automation.StationOnline ?? false;

	public AutomationView Automation => Snapshot?.Automation ??
		new AutomationView(false, AutomationStatus.Off, TimeContext.Day, false, null, false, null, null, null, false, null, false, false);

	public string SettingValue(SettingKey setting) =>
		Snapshot?.Settings.FirstOrDefault(view => view.Setting == setting)?.Value ?? "-";

	public double SettingDecimal(SettingKey setting) =>
		double.TryParse(SettingValue(setting), System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : 0;

	public bool SettingEnabled(SettingKey setting) => SettingValue(setting) == nameof(PowerState.On);

	public string HostInfo(RaspberryInfo info)
	{
		RaspberryInfoView? entry = Snapshot?.Raspberry.FirstOrDefault(view => view.Info == info);
		return entry == null || entry.Value.Length == 0 ? "-" : $"{entry.Value}{entry.Unit}";
	}

	public bool ServiceRunning(ServiceId service) =>
		Snapshot?.Services.FirstOrDefault(view => view.Service == service)?.Running ?? false;

	public MetricsView? Computer => Snapshot?.ComputerMetrics;

	public MetricsView? RaspberryMetrics => Snapshot?.RaspberryMetrics;

	// ---------- commands ----------

	public Task<string> SwitchDevice(DeviceId device, SwitchAction action) => Act(() => _client.SwitchDevice(device, action));

	public Task<string> SwitchRoom(SwitchAction action) => Act(() => _client.SwitchRoom(action));

	public Task<string> SetAutomation(SwitchAction action) => Act(() => _client.SetAutomation(action));

	public Task<string> SetSleepMode(SwitchAction action) => Act(() => _client.SetSleepMode(action));

	public Task<string> ReleaseHolds() => Act(() => _client.ReleaseHolds());

	public Task<string> SetSetting(SettingKey setting, string value) => Act(() => _client.SetSetting(setting, value));

	public Task<string> ComputerCommand(ComputerCommand command, string argument = "") => Act(() => _client.Computer(command, argument));

	public Task<string> RaspberryCommand(RaspberryCommand command, string argument = "") => Act(() => _client.Raspberry(command, argument));

	public Task<string> ControlService(ServiceId service, ServiceAction action) => Act(() => _client.ControlService(service, action));

	public Task<string> Journal(ServiceId service, int lines) => Text(_client.Journal(service, lines));

	public Task<string> Notify(NotificationChannel channel, string message) =>
		Text(_client.Notify(new NotifyRequest(message, NotificationSource.Kernel, NotificationLevel.Info, channel)));

	public async Task<IReadOnlyList<NotificationEntry>> Notifications(int limit = 200) =>
		(await _client.Notifications(limit)).Match(list => list.Entries, _ => []);

	public Task<string> ClearNotifications() => Text(_client.ClearNotifications());

	public Task<string> Ask(string message, string conversation) =>
		Text(_client.Ask(message, conversation, "Web"));

	public Task<string> ResetChat(string conversation) => Text(_client.ResetConversation(conversation));

	public async Task<IReadOnlyList<ModelView>> Models() => (await _client.Models()).Match(list => list.Models, _ => []);

	public Task<string> SetModel(string model) => Text(_client.SetModel(model));

	public Task<string> Prompt() => Text(_client.Prompt());

	public Task<string> SetPrompt(string prompt) => Text(_client.SetPrompt(prompt));

	public Task<string> ResetPrompt() => Text(_client.ResetPrompt());

	public async Task<IReadOnlyList<AiSettingView>> AiSettings() => (await _client.AiSettings()).Match(list => list.Settings, _ => []);

	public Task<string> SetAiSetting(AiSettingKey setting, double value) => Text(_client.SetAiSetting(setting, value));

	public async Task<IReadOnlyList<ScheduleView>> Schedules() => (await _client.Schedules()).Match(list => list.Schedules, _ => []);

	public Task<string> CreateSchedule(CreateScheduleRequest request) => Text(_client.CreateSchedule(request));

	public Task<string> CommandSchedule(string id, string command) => Text(_client.CommandSchedule(id, command));

	public Task<string> DeleteSchedule(string id) => Text(_client.DeleteSchedule(id));

	public async Task<HistorySeries?> History(string metric, int hours, DateTimeOffset? until = null) =>
		(await _client.History(metric, hours, until)).Match(HistorySeries? (series) => series, _ => null);

	public Task<string> PushKey() => Text(_client.PushKey());

	public Task<string> PushSubscribe(PushDeviceRequest device) => Text(_client.PushSubscribe(device));

	public Task<string> PushUnsubscribe(string endpoint) => Text(_client.PushUnsubscribe(endpoint));

	public Task<string> PushTest() => Text(_client.PushTest());

	private static async Task<string> Text(Task<Result<string>> call) => (await call).Match(value => value, error => error);

	private async Task<string> Act(Func<Task<Result<string>>> call)
	{
		string result = await Text(call());
		await Refresh();
		return result;
	}

	// ---------- live state ----------

	public IDisposable Subscribe(Action onChanged)
	{
		Changed += onChanged;
		return new Subscription(this, onChanged);
	}

	public async Task Refresh(CancellationToken token = default)
	{
		Result<CortanaSnapshot> snapshot = await _client.Snapshot(token);

		if (snapshot.IsOk) Apply(snapshot.Value);
		else
		{
			Online = false;
			Notify();
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await foreach (CortanaSnapshot snapshot in _client.SnapshotStream(stoppingToken)) Apply(snapshot);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				logger.LogInformation("The event stream is unavailable ({Message}), falling back to polling", ex.Message);
			}

			if (!await PollUntilRetry(stoppingToken)) return;
		}
	}

	private async Task<bool> PollUntilRetry(CancellationToken token)
	{
		DateTime retryAt = DateTime.UtcNow.Add(ReconnectDelay);

		while (DateTime.UtcNow < retryAt)
		{
			try
			{
				await Refresh(token);
				await Task.Delay(PollInterval, token);
			}
			catch (OperationCanceledException)
			{
				return false;
			}
			catch (Exception ex)
			{
				logger.LogWarning("Refresh failed: {Message}", ex.Message);
			}
		}

		return true;
	}

	private void Apply(CortanaSnapshot snapshot)
	{
		Snapshot = snapshot;
		Online = true;
		LastUpdate = DateTimeOffset.Now;
		Notify();
	}

	private void Notify()
	{
		foreach (Action handler in Changed?.GetInvocationList().Cast<Action>() ?? [])
		{
			try
			{
				handler();
			}
			catch (Exception ex)
			{
				logger.LogDebug("A subscriber threw: {Message}", ex.Message);
			}
		}
	}

	private void Unsubscribe(Action onChanged) => Changed -= onChanged;

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
