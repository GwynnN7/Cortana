using Newtonsoft.Json;
using QRCoder;
using SixLabors.ImageSharp.Formats.Png;

namespace Utility
{
    public static class Functions
    {
        static public T? LoadFile<T>(string Path)
        {
            T? DataToLoad = default;
            if (File.Exists(Path))
            {
                var file = File.ReadAllText(Path);
                DataToLoad = JsonConvert.DeserializeObject<T>(file);
            }
            return DataToLoad;
        }

        static public void WriteFile<T>(string FileName, T Data, JsonSerializerSettings? options = null)
        {
            var newJson = JsonConvert.SerializeObject(Data, options);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), FileName);
            var filePathBackup = Path.Combine(Directory.GetCurrentDirectory(), "../../CortanaBackup/" + FileName);
            File.WriteAllText(filePath, newJson);
            File.WriteAllText(filePathBackup, newJson);
        }

        public static Stream CreateQRCode(string content, bool useNormalColors, bool useBorders)
        {
            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);

            byte[] qrCodeAsPngByteArr;
            if (useNormalColors) qrCodeAsPngByteArr = qrCode.GetGraphic(20, drawQuietZones: useBorders);
            else qrCodeAsPngByteArr = qrCode.GetGraphic(20, lightColorRgba: new byte[] { 81, 209, 246 }, darkColorRgba: new byte[] { 52, 24, 80 }, drawQuietZones: useBorders);

            var ImageStream = new MemoryStream();
            using (var image = Image.Load(qrCodeAsPngByteArr))
            {
                image.Save(ImageStream, new PngEncoder());
            }
            return ImageStream;
        }
    }
}
