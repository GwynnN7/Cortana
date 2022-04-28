using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace RequestsHandler
{
    public enum ERequestsType
    {
        [Description("")]
        Home,

        [Description("utility/")]
        Utility,

        [Description("automation/")]
        Automation
    }

    public static class MakeRequest
    {
        private static HttpClient client;
        static MakeRequest()
        {
            if (client == null)
            {
                HttpClientHandler handler = new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                client = new HttpClient();

                string publicIP = GetPublicIP();
                string BaseURL = $"http://{publicIP}:5000/cortana-api/";

                client.BaseAddress = new Uri(BaseURL);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            }
        }

        private static string GetSubdomain(ERequestsType en)
        {
            Type type = en.GetType();

            MemberInfo[] memInfo = type.GetMember(en.ToString());
            if (memInfo != null && memInfo.Length > 0)
            {
                object[] attrs = memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
                if (attrs != null && attrs.Length > 0)
                    return ((DescriptionAttribute)attrs[0]).Description;
            }
            return en.ToString();
        }

        private static string GetPublicIP()
        {
            return new WebClient().DownloadString("https://api.ipify.org");
        }

        public static async Task<string> Check()
        {
            var response = await Execute(ERequestsType.Home, "");
            return response;
        }

        public static async Task<string> Execute(ERequestsType Type, string Command, string GetData = "")
        {
            try
            {
                string url = GetSubdomain(Type) + Command;
                if (GetData != "") url += "?" + GetData;
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    string stringResult = await response.Content.ReadAsStringAsync();
                    return stringResult;
                }
                else return $"Errore {response.StatusCode}";
            }
            catch
            {
                return "Nessuna risposta";
            }
        }
    }
}