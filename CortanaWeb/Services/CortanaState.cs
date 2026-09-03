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

	public DeviceView? Device(string device) => Snapshot?.Devices.FirstOrDefault(view => view.Device == device);

	public string DeviceStatus(string device) => Device(device)?.State.ToString() ?? "-";

	public bool IsDeviceOn(string device) => Device(device)?.State == PowerState.On;

	public SensorView? Sensor(string sensor) => Snapshot?.Sensors.FirstOrDefault(view => view.Sensor == sensor);

	public bool IsSourceOnline(string source) => Source(source)?.State == SourceState.Online;

	public bool SourcesOnline => Snapshot?.Automation.SourcesOnline ?? false;

	public Mood Mood => Snapshot?.Mood ?? Mood.Calm;

	public string MoodReason => Snapshot?.MoodReason ?? "";

	public DesktopActivity? Activity => Snapshot?.Activity;

	public AutomationView Automation => Snapshot?.Automation ??
		new AutomationView(false, AutomationStatus.Off, TimeContext.Day, false, null, false, null, null, null, false, false, null, false, false, false);

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

	public SourceView? Source(string source) =>
		(Snapshot?.Sources ?? []).FirstOrDefault(view => view.Id.Equals(source, StringComparison.OrdinalIgnoreCase));

	// ---------- commands ----------

	public Task<string> SwitchDevice(string device, SwitchAction action) => Act(() => _client.SwitchDevice(device, action));

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

	public async Task<IReadOnlyList<SourceView>> Sources() =>
		(await _client.Sources()).Match(list => list.Sources, _ => []);

	public async Task<IReadOnlyList<ChannelView>> Channels() =>
		(await _client.Channels()).Match(list => list.Channels, _ => []);

	public Task<string> RegisterDevice(VirtualDevice device) => Act(() => _client.RegisterDevice(device));

	public Task<string> RegisterSensor(VirtualSensor sensor) => Act(() => _client.RegisterSensor(sensor));

	public Task<string> Unregister(string id) => Act(() => _client.Unregister(id));

	public async Task<Registrations> Registered() =>
		(await _client.Registrations()).Match(value => value, _ => new Registrations([], []));

	public async Task<IReadOnlyList<Bind>> Binds() => (await _client.Binds()).Match(list => list.Binds, _ => []);

	public async Task<IReadOnlyList<BindStatusView>> BindStatus() => (await _client.Binds()).Match(list => list.Status, _ => []);

	public async Task<IReadOnlyList<string>> AdriftBinds() => (await _client.Binds()).Match(list => list.Adrift, _ => []);

	public async Task<IReadOnlyList<string>> AdriftWarnings() => (await _client.Warnings()).Match(list => list.Adrift, _ => []);

	public Task<string> RestoreBind(string id) => Act(() => _client.RestoreBind(id));

	public Task<string> RestoreWarning(string id) => Act(() => _client.RestoreWarning(id));

	public Task<string> SaveBind(Bind bind) => Act(() => _client.SaveBind(bind));

	public Task<string> DeleteBind(string id) => Act(() => _client.DeleteBind(id));

	public async Task<DashboardLayout> Layout() =>
		(await _client.Layout()).Match(value => value, _ => new DashboardLayout([], []));

	public Task<string> SaveLayout(DashboardLayout layout) => Text(_client.SaveLayout(layout));

	public async Task<IReadOnlyList<PluginView>> Plugins() =>
		(await _client.Plugins()).Match(list => list.Plugins, _ => []);

	public bool FeatureOn(string plugin) =>
		(Snapshot?.Plugins ?? []).FirstOrDefault(view => view.Id == plugin)?.Active ?? true;

	public Task<string> SwitchPlugin(string plugin, SwitchAction action) => Act(() => _client.SwitchPlugin(plugin, action));

	public async Task<IReadOnlyList<WarningView>> Warnings() =>
		(await _client.Warnings()).Match(list => list.Warnings, _ => []);

	public Task<string> SaveWarning(Warning warning) => Act(() => _client.SaveWarning(warning));

	public Task<string> DeleteWarning(string id) => Act(() => _client.DeleteWarning(id));

	public async Task<IReadOnlyList<MemoryEntry>> Memories() =>
		(await _client.Memories()).Match(list => list.Memories, _ => []);

	public Task<string> Remember(string text, MemoryKind kind) =>
		Text(_client.Remember(new RememberRequest(text, kind, "Web")));

	public Task<string> Forget(string id) => Text(_client.Forget(id));

	public Task<string> Ask(string message, string conversation) =>
		Text(_client.Ask(message, conversation, "Web"));

	public Task<string> ResetChat(string conversation) => Text(_client.ResetConversation(conversation));

	public async Task<IReadOnlyList<ChatTurn>> ChatHistory(string conversation) =>
		(await _client.Conversation(conversation)).Match(view => view.Turns, _ => []);

	public async Task<IReadOnlyList<Note>> Notes() => (await _client.Notes()).Match(list => list.Notes, _ => []);

	public Task<string> WriteNote(string text, NoteKind kind) => Act(() => _client.WriteNote(new NoteRequest(text, kind, "web")));

	public Task<string> SettleNote(string id, bool done) => Act(() => _client.SettleNote(id, done));

	public Task<string> DropNote(string id) => Act(() => _client.DropNote(id));

	public Task<string> ClearNotes() => Act(() => _client.ClearNotes());

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
