using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaDesktop;

internal static class Activity
{
	private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(750);
	private static readonly TimeSpan Heartbeat = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

	private static readonly Dictionary<string, ActivityCategory> Map = new(StringComparer.OrdinalIgnoreCase)
	{
		["code"] = ActivityCategory.Coding,
		["foot"] = ActivityCategory.Coding,
		["kitty"] = ActivityCategory.Coding,
		["alacritty"] = ActivityCategory.Coding,
		["com.anthropic.claude"] = ActivityCategory.Coding,
		["md.obsidian.obsidian"] = ActivityCategory.Coding,
		["jetbrains-rider"] = ActivityCategory.Coding,
		["zen"] = ActivityCategory.Browsing,
		["firefox"] = ActivityCategory.Browsing,
		["chromium"] = ActivityCategory.Browsing,
		["org.telegram.desktop"] = ActivityCategory.Browsing,
		["equibop"] = ActivityCategory.Browsing,
		["vesktop"] = ActivityCategory.Browsing,
		["tidal-hifi"] = ActivityCategory.Media,
		["spotify"] = ActivityCategory.Media,
		["mpv"] = ActivityCategory.Media,
		["vlc"] = ActivityCategory.Media,
		["gamescope"] = ActivityCategory.Gaming,
		["steam_app"] = ActivityCategory.Gaming,
		["lutris"] = ActivityCategory.Gaming,
		["heroic"] = ActivityCategory.Gaming
	};

	private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
	{
		["code"] = "VS Code",
		["foot"] = "Terminal",
		["kitty"] = "Terminal",
		["alacritty"] = "Terminal",
		["com.anthropic.claude"] = "Claude",
		["md.obsidian.obsidian"] = "Obsidian",
		["jetbrains-rider"] = "Rider",
		["zen"] = "Zen",
		["firefox"] = "Firefox",
		["chromium"] = "Chromium",
		["org.telegram.desktop"] = "Telegram",
		["equibop"] = "Discord",
		["vesktop"] = "Discord",
		["tidal-hifi"] = "TIDAL",
		["spotify"] = "Spotify",
		["mpv"] = "mpv",
		["vlc"] = "VLC",
		["lutris"] = "Lutris",
		["heroic"] = "Heroic",
		["gamescope"] = "Game",
		["steam_app"] = "Game"
	};

	private static readonly Lock Gate = new();
	private static readonly SemaphoreSlim Wake = new(0, 1);

	private const int Unknown = -1;

	private static readonly string[] IdleDaemons = ["hypridle", "swayidle"];

	private static readonly TimeSpan LockPoll = TimeSpan.FromSeconds(10);

	private static ActivityDetail _detail = ActivityDetail.NowPlaying;
	private static bool _locked;
	private static int _idleSeconds;
	private static NowPlaying? _playing;
	private static DesktopActivity? _reported;
	private static DateTimeOffset _since = DateTimeOffset.Now;
	private static DateTimeOffset _sent = DateTimeOffset.MinValue;

	public static void Start(Action<DesktopActivity> publish)
	{
		LoadOverrides();
		_ = Task.Run(() => WatchLoop(publish));
		_ = Task.Run(() => DebounceLoop(publish));
		_ = Task.Run(MusicLoop);
		_ = Task.Run(LockLoop);
	}

	public static void Resend(Action<DesktopActivity> publish)
	{
		DesktopActivity? current;
		lock (Gate) current = _reported;
		if (current != null) publish(current);
	}

	private static void LoadOverrides()
	{
		string path = CortanaEnvironment.Path_(CortanaFolder.Config, "activity.conf");
		if (!File.Exists(path)) return;

		foreach (string line in File.ReadAllLines(path))
		{
			string entry = line.Split('#')[0].Trim();
			string[] parts = entry.Split('=', 2, StringSplitOptions.TrimEntries);
			if (parts.Length != 2) continue;

			if (parts[0].Equals("detail", StringComparison.OrdinalIgnoreCase))
			{
				if (Enum.TryParse(parts[1], true, out ActivityDetail level)) _detail = level;
			}
			else if (Enum.TryParse(parts[1], true, out ActivityCategory category))
			{
				Map[parts[0]] = category;
			}
		}
	}

	private static async Task WatchLoop(Action<DesktopActivity> publish)
	{
		while (true)
		{
			try
			{
				if (!await Listen()) await Poll(publish);
			}
			catch (Exception ex)
			{
				Log.Write("Activity", $"Watch stopped: {ex.Message}");
			}

			await Task.Delay(PollInterval);
		}
	}

	private static async Task<bool> Listen()
	{
		string? signature = CortanaEnvironment.Read("HYPRLAND_INSTANCE_SIGNATURE");
		string? runtime = CortanaEnvironment.Read("XDG_RUNTIME_DIR");
		if (signature is not { Length: > 0 } || runtime is not { Length: > 0 }) return false;


		string path = Path.Combine(runtime, "hypr", signature, ".socket2.sock");
		if (!File.Exists(path)) return false;

		using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
		await socket.ConnectAsync(new UnixDomainSocketEndPoint(path));

		byte[] buffer = new byte[4096];
		Nudge();

		while (true)
		{
			int read = await socket.ReceiveAsync(buffer);
			if (read <= 0) return true;

			string chunk = Encoding.UTF8.GetString(buffer, 0, read);
			foreach (string line in chunk.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				string name = line.Split(">>")[0];
				if (name is "activewindow" or "activewindowv2" or "fullscreen" or "closewindow" or "workspace" or "focusedmon")
					Nudge();
			}
		}
	}

	private static async Task Poll(Action<DesktopActivity> publish)
	{
		for (var i = 0; i < 12; i++)
		{
			Evaluate(publish);
			await Task.Delay(PollInterval);
		}
	}

	private static async Task LockLoop()
	{
		while (true)
		{
			try
			{
				bool locked = ScreenLocked();
				int idle = IdleSeconds();

				bool changed;
				lock (Gate)
				{
					changed = locked != _locked || Math.Sign(idle) != Math.Sign(_idleSeconds);
					_locked = locked;
					_idleSeconds = idle;
				}

				if (changed) Nudge();
			}
			catch (Exception ex)
			{
				Log.Write("Activity", $"Could not read the lock state: {ex.Message}");
			}

			await Task.Delay(LockPoll);
		}
	}

	private static int IdleSeconds()
	{
		if (!IdleDaemons.Any(Running)) return Unknown;
		if (IdleInhibited()) return Unknown;

		string path = Path.Combine(CortanaEnvironment.Read("XDG_RUNTIME_DIR", "/tmp"), "cortana", "idle");
		if (!File.Exists(path)) return Unknown;

		try
		{
			if (File.ReadAllText(path).Trim() != "1") return 0;

			var since = (int)(DateTimeOffset.Now - File.GetLastWriteTime(path)).TotalSeconds;
			return Math.Max(since, 1);
		}
		catch (IOException)
		{
			return Unknown;
		}
	}

	private static bool IdleInhibited()
	{
		string json = Run("busctl", "--json=short call org.freedesktop.login1 /org/freedesktop/login1 org.freedesktop.login1.Manager ListInhibitors");
		if (json is not { Length: > 2 }) return false;

		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.GetArrayLength() == 0)
				return false;

			foreach (JsonElement entry in data[0].EnumerateArray())
			{
				if (entry.GetArrayLength() < 4) continue;

				string what = entry[0].GetString() ?? "";
				string mode = entry[3].GetString() ?? "";

				if (mode == "block" && what.Contains("idle", StringComparison.OrdinalIgnoreCase)) return true;
			}
		}
		catch (JsonException) { }

		return false;
	}

	private static bool ScreenLocked() =>
		Running("qs") && Run("qs", "-c caelestia ipc call lock isLocked")
			.Equals("true", StringComparison.OrdinalIgnoreCase);

	private static async Task MusicLoop()
	{
		while (true)
		{
			try
			{
				await FollowPlayer();
			}
			catch (Exception ex)
			{
				Log.Write("Activity", $"Music watch stopped: {ex.Message}");
			}

			lock (Gate) _playing = null;
			Nudge();
			await Task.Delay(PollInterval);
		}
	}

	private static async Task FollowPlayer()
	{
		var start = new ProcessStartInfo("playerctl")
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true
		};

		start.ArgumentList.Add("metadata");
		start.ArgumentList.Add("--follow");
		start.ArgumentList.Add("--format");
		start.ArgumentList.Add("{{status}}|{{artist}}|{{title}}|{{album}}");

		using Process? process = Process.Start(start);

		if (process == null) return;

		while (await process.StandardOutput.ReadLineAsync() is { } line)
		{
			NowPlaying? next = ParsePlayer(line);

			lock (Gate)
			{
				if (_playing == next) continue;
				_playing = next;
			}

			Nudge();
		}

		await process.WaitForExitAsync();
	}

	private static NowPlaying? ParsePlayer(string line)
	{
		string[] parts = line.Split('|');
		if (parts.Length < 4) return null;

		string status = parts[0].Trim();
		if (status is not ("Playing" or "Paused")) return null;

		string artist = parts[1].Trim();
		string title = parts[2].Trim();
		string album = parts[3].Trim();
		if (title.Length == 0 && artist.Length == 0) return null;

		return _detail == ActivityDetail.NowPlaying
			? new NowPlaying(Empty(artist), Empty(title), Empty(album), status == "Paused")
			: new NowPlaying(null, null, null, status == "Paused");
	}

	private static string? Empty(string value) => value.Length == 0 ? null : value;

	private static void Nudge()
	{
		try
		{
			if (Wake.CurrentCount == 0) Wake.Release();
		}
		catch (SemaphoreFullException) { }
	}

	private static async Task DebounceLoop(Action<DesktopActivity> publish)
	{
		while (true)
		{
			await Wake.WaitAsync(Heartbeat);
			await Task.Delay(Debounce);
			Evaluate(publish);
		}
	}

	private static void Evaluate(Action<DesktopActivity> publish)
	{
		try
		{
			(string? window, bool fullscreen) = Hyprland ? ActiveWindow() : SessionWindow();
			(ActivityCategory category, bool known) = Categorise(window);

			string? subject = null;
			if (known && _detail != ActivityDetail.CategoryOnly)
				subject = Names.TryGetValue(window!, out string? name) ? name : window;

			DesktopActivity next;
			DateTimeOffset now = DateTimeOffset.Now;

			lock (Gate)
			{
				if (_locked) category = ActivityCategory.Locked;
				else if (_idleSeconds > 0) category = ActivityCategory.Away;

				bool changed = _reported is null
					|| _reported.Category != category
					|| _reported.Subject != subject
					|| _reported.Fullscreen != fullscreen
					|| _reported.Locked != _locked
					|| Math.Sign(_reported.IdleSeconds) != Math.Sign(_idleSeconds)
					|| _reported.Playing != _playing;

				if (!changed && now - _sent < Heartbeat) return;
				if (_reported is null || _reported.Category != category) _since = now;

				next = new DesktopActivity(category, subject, null, _since, _idleSeconds, _locked, fullscreen, _playing);
				_reported = next;
				_sent = now;
			}

			publish(next);
		}
		catch (Exception ex)
		{
			Log.Write("Activity", $"Could not read the active window: {ex.Message}");
		}
	}

	private static (ActivityCategory Category, bool Known) Categorise(string? window)
	{
		if (window is not { Length: > 0 }) return (ActivityCategory.Idle, false);
		if (Map.TryGetValue(window, out ActivityCategory mapped)) return (mapped, true);

		foreach ((string prefix, ActivityCategory category) in Map)
			if (window.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
				return (category, true);

		return (ActivityCategory.Browsing, false);
	}

	private static bool Hyprland
	{
		get
		{
			string? signature = CortanaEnvironment.Read("HYPRLAND_INSTANCE_SIGNATURE");
			return signature is { Length: > 0 };
		}
	}

	private static (string? Window, bool Fullscreen) SessionWindow()
	{
		if (Running("gamescope")) return ("gamescope", true);
		if (Run("pgrep", "-f steamapps/common").Length > 0) return ("steam_app", true);

		return (null, false);
	}

	private static bool Running(string process) => Run("pgrep", $"-x {process}").Length > 0;

	private static (string? Window, bool Fullscreen) ActiveWindow()
	{
		string json = Run("hyprctl", "-j activewindow");
		if (json is not { Length: > 2 }) return (null, false);

		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;
		if (root.ValueKind != JsonValueKind.Object) return (null, false);

		string? window = root.TryGetProperty("class", out JsonElement value) ? value.GetString() : null;
		if (window is not { Length: > 0 }) return (null, false);

		var fullscreen = false;
		if (root.TryGetProperty("fullscreen", out JsonElement state))
			fullscreen = state.ValueKind switch
			{
				JsonValueKind.True => true,
				JsonValueKind.Number => state.GetInt32() > 0,
				_ => false
			};

		return (window, fullscreen);
	}

	private static string Run(string file, string arguments)
	{
		using var process = Process.Start(new ProcessStartInfo(file, arguments)
		{
			RedirectStandardOutput = true,
			RedirectStandardError = true
		});

		if (process == null) return "";

		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit(3000);
		return output.Trim();
	}
}
