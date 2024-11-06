using Newtonsoft.Json;
using QRCoder;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace Utility
{
    public static class Functions
    {
        public static T? LoadFile<T>(string path)
        {
            T? dataToLoad = default;
            if (!File.Exists(path)) return dataToLoad;
            
            string file = File.ReadAllText(path);
            dataToLoad = JsonConvert.DeserializeObject<T>(file);
            return dataToLoad;
        }

        public static void WriteFile<T>(string fileName, T data, JsonSerializerSettings? options = null)
        {
            string newJson = JsonConvert.SerializeObject(data, options);

            string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            File.WriteAllText(filePath, newJson);
        }

        public static void Log(string fileName, string log)
        {
            var path = $"/home/cortana/CortanaLogs/{fileName}.log";
            using StreamWriter logFile = File.Exists(path) ? File.AppendText(path) : File.CreateText(path);
            logFile.WriteLine($"{DateTime.Now}: {log}\n");
        }

        public static Stream CreateQrCode(string content, bool useNormalColors, bool useBorders)
        {
            var qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(qrCodeData);

            byte[] qrCodeAsPngByteArr = useNormalColors ? qrCode.GetGraphic(20, drawQuietZones: useBorders) : qrCode.GetGraphic(20, lightColorRgba: new byte[] { 81, 209, 246 }, darkColorRgba: new byte[] { 52, 24, 80 }, drawQuietZones: useBorders);

            var imageStream = new MemoryStream();
            using Image image = Image.Load(qrCodeAsPngByteArr);
            image.Save(imageStream, new PngEncoder());
            return imageStream;
        }
    }
}
