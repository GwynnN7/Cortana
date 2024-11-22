using Newtonsoft.Json;
using QRCoder;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Processor
{
    public static class Software
    {
        public static readonly SecretsData Secrets;

        static Software()
        {
            Secrets = LoadFile<SecretsData>("Storage/Config/Secrets.json");
        }
        
        public static T? LoadFile<T>(string path)
        {
            T? dataToLoad = default;
            if (!File.Exists(path)) return dataToLoad;

            try
            {
                string file = File.ReadAllText(path);
                dataToLoad = JsonConvert.DeserializeObject<T>(file);
            }
            catch (Exception ex) { throw new CortanaException(ex.Message, ex); }
            return dataToLoad;
        }

        public static string LoadHtml(string name)
        {
            const string path = "Storage/Assets/HTML";
            try {
                return File.ReadAllText($"{path}/{name}.html");
            }
            catch (Exception ex) { throw new CortanaException(ex.Message, ex); }
        }

        public static void WriteFile<T>(string fileName, T data, JsonSerializerSettings? options = null)
        {
            options ??= new JsonSerializerSettings { Formatting = Formatting.Indented };
            string newJson = JsonConvert.SerializeObject(data, options);
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            File.WriteAllText(filePath, newJson);
        }

        public static void Log(string fileName, string log)
        {
            var path = $"/home/cortana/Cortana/CortanaKernel/Log/{fileName}.log";
            using StreamWriter logFile = File.Exists(path) ? File.AppendText(path) : File.CreateText(path);
            logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
        }

        public static Stream CreateQrCode(string content, bool useNormalColors, bool useBorders)
        {
            var qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);

            byte[] qrCodeAsPngByteArr = useNormalColors ? qrCode.GetGraphic(20, drawQuietZones: useBorders) : qrCode.GetGraphic(20, lightColorRgba: [81, 209, 246], darkColorRgba: [52, 24, 80], drawQuietZones: useBorders);

            var imageStream = new MemoryStream();
            using Image image = Image.Load(qrCodeAsPngByteArr);
            image.Save(imageStream, new PngEncoder());
            return imageStream;
        }
        
        public static async Task<Stream> GetYoutubeStream(string url, EStreamType type)
        {
            var youtube = new YoutubeClient();
            StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

            IStreamInfo streamInfo = type switch
            {
                EStreamType.Audio => streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate(),
                EStreamType.Video => streamManifest.GetMuxedStreams().GetWithHighestBitrate(),
                _ => streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate(),
            };
            
            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo);
            return stream;
        }

        public static async Task<Video> GetYoutubeVideoInfos(string url)
        {
            var youtube = new YoutubeClient();

            string link = url.Split("&").First();
            var substrings = new[] { "https://www.youtube.com/watch?v=", "https://youtu.be/" };
            string? result = null;
            foreach (string sub in substrings)
            {
                if (link.StartsWith(sub)) result = link[sub.Length..];
            }

            if (result != null) return await youtube.Videos.GetAsync(result);
            IReadOnlyList<VideoSearchResult> videos = await youtube.Search.GetVideosAsync(url).CollectAsync(1);
            result = videos[0].Id;
            return await youtube.Videos.GetAsync(result);
        }
    }

    [method: JsonConstructor]
    public readonly struct SecretsData(
        string discordToken,
        string telegramToken,
        string desktopPassword,
        string igdbClient,
        string igdbSecret)
    {
        public string DiscordToken { get; } = discordToken;
        public string TelegramToken { get; } = telegramToken;
        public string DesktopPassword { get; } = desktopPassword;
        public string IgdbClient { get; } = igdbClient;
        public string IgdbSecret { get; } = igdbSecret;
    }
    
    
    [Serializable]
    public class CortanaException : Exception
    {
        public CortanaException () {}

        public CortanaException(string message) : base(message)
        {
            Software.Log("CortanaException", message);
        }

        public CortanaException(string message, Exception innerException) : base(message, innerException)
        {
            Software.Log("CortanaException", message);
        }    
    }
}
