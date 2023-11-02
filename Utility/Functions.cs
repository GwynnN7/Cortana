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
            using (var image = SixLabors.ImageSharp.Image.Load(qrCodeAsPngByteArr))
            {
                image.Save(ImageStream, new PngEncoder());
            }
            return ImageStream;
        }

        public static string RandomDice(int dices)
        {
            string dicesResults = "";
            for (int i = 0; i < dices; i++)
            {
                dicesResults += Convert.ToString(new Random().Next(1, 7)) + " ";
            }
            return dicesResults;
        }

        public static string TOC()
        {
            string result = "Testa";
            if (new Random().Next() > (int.MaxValue / 2)) result = "Croce";
            return result;
        }

        public static string RandomOption(string options)
        {
            string[] separatedList = options.Split(" ");
            int randomIndex = new Random().Next(0, separatedList.Length);
            return separatedList[randomIndex];
        }

        public static string RandomOption(string[] options)
        {
            int randomIndex = new Random().Next(0, options.Length);
            return options[randomIndex];
        }

        public static string RandomNumber(int min, int max)
        {
            string randomNumber = Convert.ToString(new Random().Next(min, max));
            return randomNumber;
        }

        public static EHardwareTrigger TriggerStateFromString(string state)
        {
            state = string.Concat(state[0].ToString().ToUpper(), state.AsSpan(1));
            Enum.TryParse(state, out EHardwareTrigger Status);
            return Status;
        }

        public static async Task<string> GetPublicIP()
        {
            using var client = new HttpClient();
            var ip = await client.GetStringAsync("https://api.ipify.org");
            return ip;
        }

        public static bool RequestPC(string url)
        {
            using var client = new HttpClient();
            var result = client.GetAsync($"http://{HardwareDriver.NetStats.Desktop_WLAN_IP}:5000/cortana-pc/{url}").Result;
            return result.IsSuccessStatusCode;
        }
    }
}