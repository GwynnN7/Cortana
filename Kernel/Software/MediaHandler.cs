using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Converter;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using CliWrap;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using Kernel.Software.Utility;

namespace Kernel.Software;

public static class MediaHandler
{
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
	
	public static async Task<MemoryStream> ExecuteFfmpeg(Stream? videoStream = null, string filePath = "")
	{
		var memoryStream = new MemoryStream();
		await Cli.Wrap("ffmpeg")
			.WithArguments($" -hide_banner -loglevel debug -i {(videoStream != null ? "pipe:0" : $"\"{filePath}\"")} -ac 2 -f s16le -ar 48000 pipe:1")
			.WithStandardInputPipe(videoStream != null ? PipeSource.FromStream(videoStream) : PipeSource.Null)
			.WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
			.ExecuteAsync();
		return memoryStream;
	}
	
	public static async Task<Stream> GetAudioStream(string url)
	{
		var youtube = new YoutubeClient();
		Video info = await GetYoutubeVideoInfos(url);
		StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(info.Url);
		IStreamInfo audioStreamInfo = GetAudioStreamInfo(streamManifest, 50);
		Stream stream = await youtube.Videos.Streams.GetAsync(audioStreamInfo);
		return stream;
	}

	public static async Task DownloadVideo(string url, EVideoQuality quality, int maxFileSize, string videoFilePath)
	{
		var youtube = new YoutubeClient();
		Video info = await GetYoutubeVideoInfos(url);
		StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(info.Url);

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
		
		await youtube.Videos.DownloadAsync([videoStreamInfo, audioStreamInfo], new ConversionRequestBuilder(Path.Combine(videoFilePath, "temp_video.mp4")).Build());
	}

	public static Stream? GetStreamFromFile(string path)
	{
		return File.Exists(path) ? File.OpenRead(path) : null;
	}

	public static async Task<Video> GetYoutubeVideoInfos(string url)
	{
		var youtube = new YoutubeClient();

		string link = url.Split("&").First();
		var substrings = new[] { "https://www.youtube.com/watch?v=", "https://youtu.be/" };
		string? result = null;
		foreach (string sub in substrings)
			if (link.StartsWith(sub))
				result = link[sub.Length..];

		if (result != null) return await youtube.Videos.GetAsync(result);
		IReadOnlyList<VideoSearchResult> videos = await youtube.Search.GetVideosAsync(url).CollectAsync(1);
		return await youtube.Videos.GetAsync(videos[0].Id);
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