using System.Collections.Concurrent;
using System.Text.Json;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Devices;
using CortanaKernel.Domain.Notifications;
using CortanaKernel.Domain.Sensors;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using WebPush;

namespace CortanaKernel.Infrastructure.Push;

/// The browser status notification
public sealed class PushService : BackgroundService
{
	private const string StatusTag = "cortana-status";
	private const string Subject = "mailto:cortana@localhost";

	private static readonly TimeSpan Safeguard = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan Coalesce = TimeSpan.FromMilliseconds(300);

	private static readonly string KeyPath = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaKernel/Vapid.json");
	private static readonly string DevicePath = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaKernel/PushDevices.json");

	private readonly WebPushClient _client = new();
	private readonly ConcurrentDictionary<string, PushDeviceRequest> _devices = new();
	private readonly Vapid _keys;

	private readonly DeviceRegistry _deviceRegistry;
	private readonly SensorRegistry _sensors;
	private readonly AutomationEngine _automation;
	private readonly AiSettingsStore _aiSettings;
	private readonly IEventBus _bus;

	private readonly SemaphoreSlim _pending = new(0, 1);
	private DateTimeOffset _overlayUntil = DateTimeOffset.MinValue;

	public PushService(
		DeviceRegistry devices,
		SensorRegistry sensors,
		AutomationEngine automation,
		AiSettingsStore aiSettings,
		IEventBus bus)
	{
		_deviceRegistry = devices;
		_sensors = sensors;
		_automation = automation;
		_aiSettings = aiSettings;
		_bus = bus;
		_keys = LoadKeys();

		foreach (PushDeviceRequest device in JsonStore.Read<List<PushDeviceRequest>>(DevicePath) ?? [])
			_devices[device.Endpoint] = device;
	}

	public string PublicKey => _keys.PublicKey;

	public int DeviceCount => _devices.Count;

	public Result<string> Subscribe(PushDeviceRequest device)
	{
		if (string.IsNullOrWhiteSpace(device.Endpoint)) return Result.Fail<string>("Missing endpoint");

		bool known = _devices.ContainsKey(device.Endpoint);
		_devices[device.Endpoint] = device;
		Save();

		_ = RefreshStatus();
		return Result.Ok(known ? "This browser was already registered" : "Browser registered");
	}

	public Result<string> Unsubscribe(string endpoint) =>
		_devices.TryRemove(endpoint, out _) ? SaveAnd("Browser removed") : Result.Fail<string>("That browser is not registered");

	private Result<string> SaveAnd(string message)
	{
		Save();
		return Result.Ok(message);
	}

	// ---------- the status line ----------

	/// Online · {lamp}{computer}{generic} · {motion}{air} {temperature}
	public string StatusLine()
	{
		var parts = new List<string> { "Online" };

		try
		{
			var devices = "";
			if (_deviceRegistry.IsOn(DeviceId.Lamp)) devices += "💡";
			if (_deviceRegistry.IsOn(DeviceId.Computer)) devices += "🖥️";
			if (_deviceRegistry.IsOn(DeviceId.Generic)) devices += "🔌";
			if (devices.Length > 0) parts.Add(devices);

			AutomationView view = _automation.View();
			var readings = "";

			if (view.SleepMode) readings += "💤";
			else if (view.Enabled) readings += view.MotionActive ? "🔆" : "💠";

			if (view.AirQualityWarning) readings += "💨";
			if (_sensors.Temperature is { } temperature) readings += $" {Units.Number(temperature)}°";

			if (readings.Length > 0) parts.Add(readings.Trim());
		}
		catch (Exception ex)
		{
			Log.Error("Push", $"The status line failed: {ex.Message}");
		}

		return string.Join(" · ", parts);
	}

	private const int MaxOverlayLength = 80;

	/// The overlay replaces a one-line status, so anything long is trimmed
	private static string Shorten(string message)
	{
		string single = message.ReplaceLineEndings(" ").Trim();
		return single.Length <= MaxOverlayLength ? single : string.Concat(single.AsSpan(0, MaxOverlayLength - 1), "…");
	}

	public Task RefreshStatus() => Deliver("", StatusLine(), device => device.StatusNotification, StatusTag, _ => false);

	/// An accepted event replaces the status body for a configured moment, then the newest status is rebuilt from live state
	public async Task ShowEvent(NotificationEntry entry)
	{
		bool Wants(PushDeviceRequest device) =>
			device.StatusNotification &&
			(!device.AlertsOnly || entry.Level != NotificationLevel.Info) &&
			(device.Sources is not { Count: > 0 } sources || sources.Contains(entry.Source.ToString()));

		if (!_devices.Values.Any(Wants))
		{
			await RefreshStatus();
			return;
		}

		await Deliver("", Shorten(entry.Message), Wants, StatusTag, device => device.Vibrate);

		var hold = TimeSpan.FromSeconds(Math.Clamp(_aiSettings.Number(AiSettingKey.PushEventSeconds), 1, 120));
		DateTimeOffset until = DateTimeOffset.Now + hold;
		_overlayUntil = until;

		_ = Task.Run(async () =>
		{
			await Task.Delay(hold);
			// A newer event may have taken over the overlay and it owns the restore
			if (_overlayUntil == until) await RefreshStatus();
		});
	}

	// ---------- lifecycle ----------

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		_bus.SubscribeAll(_ => { try { _pending.Release(); } catch (SemaphoreFullException) { /* already queued */ } });

		await RefreshStatus();

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _pending.WaitAsync(Safeguard, stoppingToken);
				await Task.Delay(Coalesce, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			// While an event overlay is up, its own restore is what puts the newest status back
			if (DateTimeOffset.Now < _overlayUntil) continue;

			await RefreshStatus();
		}
	}

	// ---------- delivery ----------

	private async Task Deliver(string title, string body, Func<PushDeviceRequest, bool> wants, string? tag, Func<PushDeviceRequest, bool> vibrates)
	{
		if (_devices.IsEmpty || string.IsNullOrWhiteSpace(_keys.PrivateKey)) return;

		var details = new VapidDetails(Subject, _keys.PublicKey, _keys.PrivateKey);
		var stale = new List<string>();

		foreach (PushDeviceRequest device in _devices.Values)
		{
			if (!wants(device)) continue;

			bool vibrate = vibrates(device);

			var payload = new Dictionary<string, object?>
			{
				["title"] = title,
				["body"] = body,
				["tag"] = tag,
				["silent"] = !vibrate,
				["ongoing"] = tag != null,
				["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
			};

			if (vibrate) payload["vibrate"] = new[] { 60 };

			try
			{
				await _client.SendNotificationAsync(
					new PushSubscription(device.Endpoint, device.P256dh, device.Auth),
					JsonSerializer.Serialize(payload), details);
			}
			catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
			{
				stale.Add(device.Endpoint);
			}
			catch (Exception ex)
			{
				Log.Error("Push", $"Send failed: {ex.Message}");
			}
		}

		if (stale.Count == 0) return;

		foreach (string endpoint in stale) _devices.TryRemove(endpoint, out _);
		Save();
		Log.Write("Push", $"Dropped {stale.Count} expired browser(s)");
	}

	private void Save() => JsonStore.Write(DevicePath, _devices.Values.ToList());

	private sealed record Vapid(string PublicKey, string PrivateKey)
	{
		public Vapid() : this("", "") { }
	}

	private static Vapid LoadKeys()
	{
		Vapid? stored = JsonStore.Read<Vapid>(KeyPath);
		if (stored is { PublicKey.Length: > 0, PrivateKey.Length: > 0 }) return stored;

		VapidDetails generated = VapidHelper.GenerateVapidKeys();
		var keys = new Vapid(generated.PublicKey, generated.PrivateKey);

		JsonStore.Write(KeyPath, keys);
		Log.Write("Push", "Generated a new VAPID key pair");
		return keys;
	}
}

/// Bridges the notification domain to the browser status notification
public sealed class WebPushSink(Lazy<PushService> push) : INotificationSink
{
	public NotificationChannel Channel => NotificationChannel.Web;

	public Task Deliver(NotificationEntry entry, CancellationToken token = default) => push.Value.ShowEvent(entry);
}

/// Telegram and Discord run in their own processes, so their notifications go out over the event stream
public sealed class StreamNotificationSink(NotificationChannel channel, CortanaKernel.Application.StateBroadcaster broadcaster) : INotificationSink
{
	public NotificationChannel Channel { get; } = channel;

	public Task Deliver(NotificationEntry entry, CancellationToken token = default)
	{
		broadcaster.Push(Channel, entry);
		return Task.CompletedTask;
	}
}
