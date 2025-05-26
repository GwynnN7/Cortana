using CortanaLib.Structures;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace CortanaLib;

public class AudioTrack {
	public required string OriginalUrl { get; init; }
	public required string StreamUrl { get; init; }
	public required string Title { get; init; } 
	public required string ThumbnailUrl { get; init; }
	public TimeSpan Duration { get; init; }
}

public static class MediaHandler
{
	private static readonly YoutubeClient YoutubeClient = new();
	
	public static Stream CreateQrCode(string content, bool useNormalColors, bool useBorders)
	{
		var qrGenerator = new QRCodeGenerator();
		QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
		var qrCode = new PngByteQRCode(qrCodeData);

		byte[] qrCodeAsPngByteArr =
			useNormalColors ? qrCode.GetGraphic(20, useBorders) : qrCode.GetGraphic(20, lightColorRgba: [81, 209, 246], darkColorRgba: [52, 24, 80], drawQuietZones: useBorders);

		var imageStream = new MemoryStream();
		using Image image = Image.Load(qrCodeAsPngByteArr);
		image.Save(imageStream, new PngEncoder());
		return imageStream;
	}

	private static async Task<VideoId> GetVideoId(string video)
	{
		VideoId? result = VideoId.TryParse(video);
		if (result.HasValue) return result.Value;
		IReadOnlyList<VideoSearchResult> videos = await YoutubeClient.Search.GetVideosAsync(video).CollectAsync(1);
		return videos[0].Id;
	}
	
	public static async Task<AudioTrack?> GetAudioTrack(string url)
	{
		Video video = await YoutubeClient.Videos.GetAsync(await GetVideoId(url));
		StreamManifest manifest = await YoutubeClient.Videos.Streams.GetManifestAsync(video.Id);

		AudioOnlyStreamInfo? audioStreamInfo = manifest
			.GetAudioOnlyStreams()
			.OrderByDescending(s => s.Bitrate)
			.FirstOrDefault();

		if (audioStreamInfo == null) return null;

		return new AudioTrack {
			Title = video.Title,
			OriginalUrl = video.Url,
			StreamUrl = audioStreamInfo.Url,
			Duration = video.Duration ?? TimeSpan.Zero,
			ThumbnailUrl = video.Thumbnails[^1].Url
		};
	}

	public static async Task<Stream> GetAudioStream(string url)
	{
		Video video = await YoutubeClient.Videos.GetAsync(await GetVideoId(url));
		StreamManifest streamManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(video.Url);
		
		IStreamInfo audioStreamInfo = GetAudioStreamInfo(streamManifest, 50);
		Stream stream = await YoutubeClient.Videos.Streams.GetAsync(audioStreamInfo);
		return stream;
	}
	
	public static async Task DownloadVideo(string url, EVideoQuality quality, int maxFileSize, string videoFilePath)
	{
		Video video = await YoutubeClient.Videos.GetAsync(await GetVideoId(url));
		StreamManifest streamManifest = await YoutubeClient.Videos.Streams.GetManifestAsync(video.Url);

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
		
		await YoutubeClient.Videos.DownloadAsync([videoStreamInfo, audioStreamInfo], new ConversionRequestBuilder(Path.Combine(videoFilePath, "temp_video.mp4")).Build());
	}

	public static Stream? GetStreamFromFile(string path)
	{
		return File.Exists(path) ? File.OpenRead(path) : null;
	}
	
	private static IStreamInfo GetVideoStreamInfo(StreamManifest streamManifest, double maxVideoSize)
	{
		IVideoStreamInfo videoStreamInfo = streamManifest
			.GetVideoStreams()
			.Where(s => s.Container == Container.Mp4)
			.Where(s => s.Size.MegaBytes < maxVideoSize)
			.GetWithHighestVideoQuality();
		return videoStreamInfo;
	}

	private static IStreamInfo GetAudioStreamInfo(StreamManifest streamManifest, double maxAudioSize)
	{
		IStreamInfo audioStreamInfo = streamManifest
			.GetAudioStreams()
			.Where(s => s.Container == Container.Mp4)
			.Where(s => s.Size.MegaBytes < maxAudioSize)
			.GetWithHighestBitrate();

		return audioStreamInfo;
	}
}