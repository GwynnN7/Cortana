using System.Diagnostics;
using System.Text.Json;
using CortanaLib.Structures;
using QRCoder;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace CortanaLib;

public class AudioTrack
{
	public required string OriginalUrl { get; init; }
	public required string StreamUrl { get; init; }
	public required string Title { get; init; }
	public required string ThumbnailUrl { get; init; }
	public TimeSpan Duration { get; init; }
}

public static class MediaHandler
{
	private const int SearchCandidates = 5;
	private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(60);
	private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

	private static readonly YoutubeClient YoutubeClient = new();
	private static readonly HttpClient MediaClient = new() { Timeout = TimeSpan.FromMinutes(10) };
	private static readonly string? YtDlp = ResolveYtDlp();

	private static readonly byte[] CortanaLight = [81, 209, 246, 255];
	private static readonly byte[] CortanaDark = [52, 24, 80, 255];

	public static bool UsesYtDlp => YtDlp != null;

	private static string? ResolveYtDlp()
	{
		string? configured = Environment.GetEnvironmentVariable("CORTANA_YTDLP");
		if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		var candidates = new List<string> { Path.Combine(home, ".local", "bin", "yt-dlp"), "/usr/local/bin/yt-dlp", "/usr/bin/yt-dlp" };

		string path = Environment.GetEnvironmentVariable("PATH") ?? "";
		candidates.AddRange(path.Split(Path.PathSeparator).Where(dir => dir.Length > 0).Select(dir => Path.Combine(dir, "yt-dlp")));

		string? found = candidates.FirstOrDefault(File.Exists);
		DataHandler.Log(found != null ? $"[Media] Using yt-dlp at {found}" : "[Media] yt-dlp not found, falling back to YoutubeExplode");
		return found;
	}

	public static Stream CreateQrCode(string content, bool useNormalColors, bool useBorders)
	{
		using var generator = new QRCodeGenerator();
		using QRCodeData data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
		var qrCode = new PngByteQRCode(data);

		byte[] png = useNormalColors
			? qrCode.GetGraphic(20, useBorders)
			: qrCode.GetGraphic(20, darkColorRgba: CortanaDark, lightColorRgba: CortanaLight, drawQuietZones: useBorders);

		return new MemoryStream(png, writable: false);
	}

	public static Stream? GetStreamFromFile(string path) => File.Exists(path) ? File.OpenRead(path) : null;

	public static async Task<AudioTrack?> GetAudioTrack(string query)
	{
		if (YtDlp != null)
		{
			try
			{
				AudioTrack? track = await YtDlpTrack(query);
				if (track != null) return track;
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Media] yt-dlp failed for '{query}': {ex.Message}");
			}
		}

		return await ExplodeTrack(query);
	}

	public static async Task<Stream> GetAudioStream(string query)
	{
		if (YtDlp != null)
		{
			AudioTrack? track = await GetAudioTrack(query);
			if (track != null) return await MediaClient.GetStreamAsync(track.StreamUrl);
		}

		Video video = await YoutubeClient.Videos.GetAsync(await GetVideoId(query));
		StreamManifest manifest = await YoutubeClient.Videos.Streams.GetManifestAsync(video.Id);
		return await YoutubeClient.Videos.Streams.GetAsync(GetAudioStreamInfo(manifest, 50));
	}

	public static async Task DownloadVideo(string query, EVideoQuality quality, int maxFileSize, string videoFilePath)
	{
		string target = Path.Combine(videoFilePath, "temp_video.mp4");

		if (YtDlp != null)
		{
			string sort = quality switch
			{
				EVideoQuality.BestVideo => "res,vbr,abr",
				EVideoQuality.BestAudio => "abr,res",
				EVideoQuality.Balanced => "res:720,abr",
				_ => throw new CortanaException("Unknown Video Quality")
			};

			await RunYtDlp(
			[
				"--no-playlist", "--no-warnings", "--quiet", "--no-progress",
				"-f", "bv*+ba/b", "-S", sort,
				"--merge-output-format", "mp4",
				"--max-filesize", $"{maxFileSize}M",
				"-o", target,
				BuildInput(query)
			], DownloadTimeout);

			if (File.Exists(target)) return;
			DataHandler.Log("[Media] yt-dlp produced no file, falling back to YoutubeExplode");
		}

		Video video = await YoutubeClient.Videos.GetAsync(await GetVideoId(query));
		StreamManifest streamManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(video.Id);

		IStreamInfo videoStreamInfo, audioStreamInfo;
		switch (quality)
		{
			case EVideoQuality.BestVideo:
				videoStreamInfo = GetVideoStreamInfo(streamManifest, maxFileSize);
				audioStreamInfo = GetAudioStreamInfo(streamManifest, maxFileSize - videoStreamInfo.Size.MegaBytes);
				break;
			case EVideoQuality.BestAudio:
				audioStreamInfo = GetAudioStreamInfo(streamManifest, maxFileSize);
				videoStreamInfo = GetVideoStreamInfo(streamManifest, maxFileSize - audioStreamInfo.Size.MegaBytes);
				break;
			case EVideoQuality.Balanced:
				videoStreamInfo = GetVideoStreamInfo(streamManifest, maxFileSize * 0.75);
				audioStreamInfo = GetAudioStreamInfo(streamManifest, maxFileSize - videoStreamInfo.Size.MegaBytes);
				break;
			default:
				throw new CortanaException("Unknown Video Quality");
		}

		await YoutubeClient.Videos.DownloadAsync([videoStreamInfo, audioStreamInfo], new ConversionRequestBuilder(target).Build());
	}

	private static string BuildInput(string query) =>
		VideoId.TryParse(query).HasValue || query.StartsWith("http", StringComparison.OrdinalIgnoreCase)
			? query
			: $"ytsearch1:{query}";

	private static async Task<AudioTrack?> YtDlpTrack(string query)
	{
		string json = await RunYtDlp(
		[
			"--no-playlist", "--no-warnings", "--quiet", "--skip-download",
			"--dump-single-json", "-f", "bestaudio/best",
			BuildInput(query)
		], ResolveTimeout);

		if (string.IsNullOrWhiteSpace(json)) return null;

		using JsonDocument document = JsonDocument.Parse(json);
		JsonElement root = document.RootElement;

		if (root.TryGetProperty("entries", out JsonElement entries) && entries.ValueKind == JsonValueKind.Array)
		{
			if (entries.GetArrayLength() == 0) return null;
			root = entries[0];
		}

		string? stream = Text(root, "url") ?? RequestedDownloadUrl(root);
		if (stream == null) return null;

		double seconds = root.TryGetProperty("duration", out JsonElement duration) && duration.ValueKind == JsonValueKind.Number
			? duration.GetDouble()
			: 0;

		return new AudioTrack
		{
			Title = Text(root, "title") ?? "Unknown",
			OriginalUrl = Text(root, "webpage_url") ?? query,
			StreamUrl = stream,
			Duration = TimeSpan.FromSeconds(seconds),
			ThumbnailUrl = Text(root, "thumbnail") ?? ""
		};
	}

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
			try { process.Kill(entireProcessTree: true); } catch { }
			throw new CortanaException($"yt-dlp timed out after {timeout.TotalSeconds:0}s");
		}

		string output = await stdout;
		string errors = await stderr;

		if (process.ExitCode == 0) return output;
		throw new CortanaException(string.IsNullOrWhiteSpace(errors) ? $"yt-dlp exited {process.ExitCode}" : errors.Trim().Split('\n')[^1]);
	}

	private static async Task<IReadOnlyList<VideoId>> ResolveCandidates(string video)
	{
		VideoId? direct = VideoId.TryParse(video);
		if (direct.HasValue) return [direct.Value];

		IReadOnlyList<VideoSearchResult> videos = await YoutubeClient.Search.GetVideosAsync(video).CollectAsync(SearchCandidates);
		if (videos.Count == 0) throw new CortanaException($"No YouTube result for '{video}'");
		return videos.Select(result => result.Id).ToList();
	}

	private static async Task<VideoId> GetVideoId(string video) => (await ResolveCandidates(video))[0];

	private static async Task<AudioTrack?> ExplodeTrack(string url)
	{
		IReadOnlyList<VideoId> candidates = await ResolveCandidates(url);
		Exception? lastFailure = null;

		foreach (VideoId candidate in candidates)
		{
			try
			{
				AudioTrack? track = await BuildTrack(candidate);
				if (track != null) return track;
			}
			catch (Exception ex)
			{
				lastFailure = ex;
				DataHandler.Log($"[Media] Candidate {candidate} unusable: {ex.Message}");
			}
		}

		if (candidates.Count == 1 && lastFailure != null) throw lastFailure;
		return null;
	}

	private static async Task<AudioTrack?> BuildTrack(VideoId id)
	{
		Video video = await YoutubeClient.Videos.GetAsync(id);
		StreamManifest manifest = await YoutubeClient.Videos.Streams.GetManifestAsync(id);

		AudioOnlyStreamInfo? audioStreamInfo = manifest
			.GetAudioOnlyStreams()
			.OrderByDescending(s => s.Bitrate)
			.FirstOrDefault();

		if (audioStreamInfo == null) return null;

		return new AudioTrack
		{
			Title = video.Title,
			OriginalUrl = video.Url,
			StreamUrl = audioStreamInfo.Url,
			Duration = video.Duration ?? TimeSpan.Zero,
			ThumbnailUrl = video.Thumbnails.Count > 0 ? video.Thumbnails[^1].Url : ""
		};
	}

	private static IStreamInfo GetVideoStreamInfo(StreamManifest streamManifest, double maxVideoSize) =>
		streamManifest.GetVideoStreams()
			.Where(s => s.Container == Container.Mp4)
			.Where(s => s.Size.MegaBytes < maxVideoSize)
			.GetWithHighestVideoQuality();

	private static IStreamInfo GetAudioStreamInfo(StreamManifest streamManifest, double maxAudioSize) =>
		streamManifest.GetAudioStreams()
			.Where(s => s.Container == Container.Mp4)
			.Where(s => s.Size.MegaBytes < maxAudioSize)
			.GetWithHighestBitrate();
}
