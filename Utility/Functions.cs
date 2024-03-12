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
            if (File.Exists(path))
            {
                var file = File.ReadAllText(path);
                dataToLoad = JsonConvert.DeserializeObject<T>(file);
            }
            return dataToLoad;
        }

        public static void WriteFile<T>(string fileName, T data, JsonSerializerSettings? options = null)
        {
            var newJson = JsonConvert.SerializeObject(data, options);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
            File.WriteAllText(filePath, newJson);
        }

        public static void Log(string fileName, string log)
        {
            string path = $"/home/cortana/CortanaLogs/{fileName}.log";
            using StreamWriter logFile = File.Exists(path) ? File.AppendText(path) : File.CreateText(path);
            logFile.WriteLine($"{DateTime.Now}: {log}\n");
        }

        public static Stream CreateQRCode(string content, bool useNormalColors, bool useBorders)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

            byte[] qrCodeAsPngByteArr;
            if (useNormalColors) qrCodeAsPngByteArr = qrCode.GetGraphic(20, drawQuietZones: useBorders);
            else qrCodeAsPngByteArr = qrCode.GetGraphic(20, lightColorRgba: new byte[] { 81, 209, 246 }, darkColorRgba: new byte[] { 52, 24, 80 }, drawQuietZones: useBorders);

            var imageStream = new MemoryStream();
            using (var image = Image.Load(qrCodeAsPngByteArr))
            {
                image.Save(imageStream, new PngEncoder());
            }
            return imageStream;
        }
    }
}
