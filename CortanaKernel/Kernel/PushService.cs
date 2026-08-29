using System.Collections.Concurrent;
using System.Text.Json;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Structures;
using WebPush;

namespace CortanaKernel.Kernel;

public static class PushService
{
	private const string Subject = "mailto:cortana@localhost";

	private static readonly string KeyPath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Vapid.json");
	private static readonly string DevicePath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/PushDevices.json");

	private static readonly WebPushClient Client = new();
	private static readonly ConcurrentDictionary<string, PostPushDevice> Devices = new();
	private static readonly Lock Gate = new();

	private sealed class Vapid
	{
		public string PublicKey { get; set; } = "";
		public string PrivateKey { get; set; } = "";
	}

	private static Vapid _keys = LoadKeys();

	public static string PublicKey => _keys.PublicKey;

	public static int DeviceCount => Devices.Count;

	static PushService()
	{
		foreach (PostPushDevice device in LoadDevices()) Devices[device.Endpoint] = device;
	}

	private static Vapid LoadKeys()
	{
		try
		{
			if (File.Exists(KeyPath))
			{
				var stored = JsonSerializer.Deserialize<Vapid>(File.ReadAllText(KeyPath), DataHandler.SerializerOptions);
				if (stored is { PublicKey.Length: > 0, PrivateKey.Length: > 0 }) return stored;
			}
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Push] Could not read the VAPID keys: {ex.Message}");
		}

		VapidDetails generated = VapidHelper.GenerateVapidKeys();
		var keys = new Vapid { PublicKey = generated.PublicKey, PrivateKey = generated.PrivateKey };

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(KeyPath)!);
			File.WriteAllText(KeyPath, JsonSerializer.Serialize(keys, DataHandler.SerializerOptions));
			DataHandler.Log("[Push] Generated a new VAPID key pair");
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Push] Could not save the VAPID keys: {ex.Message}");
		}

		return keys;
	}

	private static List<PostPushDevice> LoadDevices()
	{
		try
		{
			if (File.Exists(DevicePath))
				return JsonSerializer.Deserialize<List<PostPushDevice>>(File.ReadAllText(DevicePath), DataHandler.SerializerOptions) ?? [];
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Push] Could not read the devices: {ex.Message}");
		}

		return [];
	}

	private static void SaveDevices()
	{
		lock (Gate)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(DevicePath)!);
				File.WriteAllText(DevicePath, JsonSerializer.Serialize(Devices.Values.ToList(), DataHandler.SerializerOptions));
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Push] Could not save the devices: {ex.Message}");
			}
		}
	}

	public static StringResult Subscribe(PostPushDevice device)
	{
		if (string.IsNullOrWhiteSpace(device.Endpoint)) return StringResult.Failure("Missing endpoint");

		bool known = Devices.ContainsKey(device.Endpoint);
		Devices[device.Endpoint] = device;
		SaveDevices();

		return StringResult.Success(known ? "Device already registered" : "Device registered");
	}

	public static StringResult Unsubscribe(string endpoint)
	{
		if (!Devices.TryRemove(endpoint, out _)) return StringResult.Failure("Device not registered");

		SaveDevices();
		return StringResult.Success("Device removed");
	}

	private const string StatusTag = "cortana-status";
	private const string Title = "Cortana Online";

	private static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(15);

	private static System.Threading.Timer? _revert;
	private static System.Threading.Timer? _pulse;
	private static DateTime _holdUntil;

	/// Refreshes the status notification periodically so Android's own timestamp stays honest.
	public static void StartHeartbeat()
	{
		_pulse?.Dispose();
		_pulse = new System.Threading.Timer(_ =>
		{
			if (DateTime.Now < _holdUntil) return;
			_ = RefreshStatus();
		}, null, Heartbeat, Heartbeat);
	}

	public static async Task Send(ELogSource source, string body, bool alert)
	{
		bool Wanted(PostPushDevice device) =>
			(!device.AlertsOnly || alert) &&
			(device.Sources is not { Count: > 0 } || device.Sources.Contains(source.ToString()));

		await Deliver($"Cortana · {source}", body, device => Wanted(device) && !device.Sticky, null, _ => true);
		await Deliver(Title, body, device => Wanted(device) && device.Sticky, StatusTag, device => device.Vibrate);

		HoldThenRevert();
	}

	public static Task Broadcast(string title, string body) => Deliver(title, body, _ => true, null, _ => true);

	public static Task RefreshStatus() =>
		Deliver(Title, StatusLine(), device => device.Sticky, StatusTag, _ => false);

	private static void HoldThenRevert()
	{
		if (Devices.Values.All(device => !device.Sticky)) return;

		TimeSpan hold = TimeSpan.FromMinutes(Math.Clamp(AiSettings.NotifyMinutes, 0.5, 120));
		_holdUntil = DateTime.Now + hold;

		_revert?.Dispose();
		_revert = new System.Threading.Timer(_ => _ = RefreshStatus(), null, hold, Timeout.InfiniteTimeSpan);
	}

	public static string StatusLine()
	{
		var parts = new List<string>();

		try
		{
			string? temperature = HardwareApi.Sensors.GetAllData()
				.FirstOrDefault(sensor => sensor.Sensor == nameof(ESensor.Temperature))?.Value;

			bool motion = HardwareApi.Sensors.GetAllData()
				.FirstOrDefault(sensor => sensor.Sensor == nameof(ESensor.Motion))?.Value
				.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

			var head = new List<string>();
			if (!string.IsNullOrWhiteSpace(temperature)) head.Add($"{temperature}°C");
			if (motion) head.Add("🖲");
			if (head.Count > 0) parts.Add(string.Join(" ", head));

			IReadOnlyList<DeviceResponse> devices = HardwareApi.Devices.GetAllPower();

			bool On(EDevice device) =>
				devices.FirstOrDefault(entry => entry.Device == device.ToString())?.Status
					.Equals(nameof(EStatus.On), StringComparison.OrdinalIgnoreCase) ?? false;

			var lit = new List<string>();
			if (On(EDevice.Lamp)) lit.Add("💡");
			if (On(EDevice.Computer)) lit.Add("🖥️");
			if (On(EDevice.Generic)) lit.Add("🔌");

			if (lit.Count > 0) parts.Add(string.Join(" ", lit));
		}
		catch (Exception)
		{
		}

		return parts.Count > 0 ? string.Join("  ·  ", parts) : Title;
	}

	private static async Task Deliver(string title, string body, Func<PostPushDevice, bool> wants, string? tag,
		Func<PostPushDevice, bool> alerts)
	{
		if (Devices.IsEmpty || string.IsNullOrWhiteSpace(_keys.PrivateKey)) return;

		var details = new VapidDetails(Subject, _keys.PublicKey, _keys.PrivateKey);
		var stale = new List<string>();

		foreach (PostPushDevice device in Devices.Values)
		{
			if (!wants(device)) continue;

			bool alert = alerts(device);
			string payload = JsonSerializer.Serialize(new
			{
				title, body, tag,
				silent = !alert,
				ongoing = tag != null,
				vibrate = alert ? new[] { 60 } : []
			});

			try
			{
				await Client.SendNotificationAsync(
					new WebPush.PushSubscription(device.Endpoint, device.P256dh, device.Auth), payload, details);
			}
			catch (WebPushException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Gone)
			{
				stale.Add(device.Endpoint);
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Push] Send failed: {ex.Message}");
			}
		}

		if (stale.Count == 0) return;

		foreach (string endpoint in stale) Devices.TryRemove(endpoint, out _);
		SaveDevices();
		DataHandler.Log($"[Push] Dropped {stale.Count} expired device(s)");
	}
}
