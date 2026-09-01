using System.Diagnostics;
using System.Text.Json;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using QRCoder;

namespace CortanaLib.Media;

public sealed record AudioTrack(string Title, string OriginalUrl, string StreamUrl, TimeSpan Duration, string ThumbnailUrl);

/// YouTube access goes through the system yt-dlp
public static class MediaLibrary
{
	private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

	private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };
	private static readonly string? YtDlp = Resolve();

	private static readonly byte[] CortanaLight = [81, 209, 246, 255];
	private static readonly byte[] CortanaDark = [52, 24, 80, 255];

	public static bool YoutubeAvailable => YtDlp != null;

	private static string? Resolve()
	{
		string? configured = CortanaEnvironment.Read("CORTANA_YTDLP");
		if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var candidates = new List<string>
		{
			Path.Combine(home, ".local", "bin", "yt-dlp"),
			"/usr/local/bin/yt-dlp",
			"/usr/bin/yt-dlp"
		};

		candidates.AddRange(CortanaEnvironment.Read("PATH", "")
			.Split(Path.PathSeparator)
			.Where(directory => directory.Length > 0)
			.Select(directory => Path.Combine(directory, "yt-dlp")));

		string? found = candidates.FirstOrDefault(File.Exists);
		Log.Write("Media", found != null ? $"Using yt-dlp at {found}" : "yt-dlp is not installed, YouTube features are unavailable");
		return found;
	}

	public static Stream CreateQrCode(string content, bool classicColors, bool quietZone)
	{
		using var generator = new QRCodeGenerator();
		using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
		var code = new PngByteQRCode(data);

		byte[] png = classicColors
			? code.GetGraphic(20, quietZone)
			: code.GetGraphic(20, darkColorRgba: CortanaDark, lightColorRgba: CortanaLight, drawQuietZones: quietZone);

		return new MemoryStream(png, writable: false);
	}

	public static async Task<Result<AudioTrack>> ResolveTrack(string query)
	{
		if (YtDlp == null) return Result.Fail<AudioTrack>("yt-dlp is not installed on this machine");

		try
		{
			string json = await RunYtDlp(
			[
				"--no-playlist", "--no-warnings", "--quiet", "--skip-download",
				"--dump-single-json", "-f", "bestaudio/best",
				Input(query)
			], ResolveTimeout);

			if (string.IsNullOrWhiteSpace(json)) return Result.Fail<AudioTrack>("No result for that search");

			using JsonDocument document = JsonDocument.Parse(json);
			JsonElement root = document.RootElement;

			if (root.TryGetProperty("entries", out JsonElement entries) && entries.ValueKind == JsonValueKind.Array)
			{
				if (entries.GetArrayLength() == 0) return Result.Fail<AudioTrack>("No result for that search");
				root = entries[0];
			}

			string? stream = Text(root, "url") ?? RequestedDownloadUrl(root);
			if (stream == null) return Result.Fail<AudioTrack>("That video has no playable stream");

			double seconds = root.TryGetProperty("duration", out JsonElement duration) && duration.ValueKind == JsonValueKind.Number
				? duration.GetDouble()
				: 0;

			return Result.Ok(new AudioTrack(
				Text(root, "title") ?? "Unknown",
				Text(root, "webpage_url") ?? query,
				stream,
				TimeSpan.FromSeconds(seconds),
				Text(root, "thumbnail") ?? ""));
		}
		catch (Exception ex)
		{
			return Result.Fail<AudioTrack>(ex.Message);
		}
	}

	public static async Task<Result<Stream>> OpenAudioStream(string query)
	{
		Result<AudioTrack> track = await ResolveTrack(query);
		if (!track.IsOk) return Result.Fail<Stream>(track.Error);

		try
		{
			return Result.Ok(await Http.GetStreamAsync(track.Value.StreamUrl));
		}
		catch (Exception ex)
		{
			return Result.Fail<Stream>($"Could not open the audio stream: {ex.Message}");
		}
	}

	public static async Task<Result<string>> DownloadVideo(string query, VideoQuality quality, int maxMegabytes, string folder)
	{
		if (YtDlp == null) return Result.Fail<string>("yt-dlp is not installed on this machine");

		string target = Path.Combine(folder, "temp_video.mp4");
		string sort = quality switch
		{
			VideoQuality.BestVideo => "res,vbr,abr",
			VideoQuality.BestAudio => "abr,res",
			_ => "res:720,abr"
		};

		try
		{
			await RunYtDlp(
			[
				"--no-playlist", "--no-warnings", "--quiet", "--no-progress",
				"-f", "bv*+ba/b", "-S", sort,
				"--merge-output-format", "mp4",
				"--max-filesize", $"{maxMegabytes}M",
				"-o", target,
				Input(query)
			], DownloadTimeout);
		}
		catch (Exception ex)
		{
			return Result.Fail<string>(ex.Message);
		}

		return File.Exists(target)
			? Result.Ok(target)
			: Result.Fail<string>($"Nothing was downloaded, the video is probably larger than {maxMegabytes} MB");
	}

	private static string Input(string query) =>
		query.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? query : $"ytsearch1:{query}";

	private static string? RequestedDownloadUrl(JsonElement root)
	{
		if (!root.TryGetProperty("requested_downloads", out JsonElement downloads)) return null;
		if (downloads.ValueKind != JsonValueKind.Array || downloads.GetArrayLength() == 0) return null;
		return Text(downloads[0], "url");
	}

	private static string? Text(JsonElement element, string property) =>
		element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
			? value.GetString()
			: null;

	private static async Task<string> RunYtDlp(string[] arguments, TimeSpan timeout)
	{
		var info = new ProcessStartInfo
		{
			FileName = YtDlp!,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		foreach (string argument in arguments) info.ArgumentList.Add(argument);

		using var process = new Process { StartInfo = info };
		process.Start();

		Task<string> stdout = process.StandardOutput.ReadToEndAsync();
		Task<string> stderr = process.StandardError.ReadToEndAsync();

		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
			throw new CortanaException($"yt-dlp timed out after {timeout.TotalSeconds:0}s");
		}

		string output = await stdout;
		string errors = await stderr;

		if (process.ExitCode == 0) return output;
		throw new CortanaException(string.IsNullOrWhiteSpace(errors) ? $"yt-dlp exited {process.ExitCode}" : errors.Trim().Split('\n')[^1]);
	}
}
