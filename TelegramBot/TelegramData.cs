using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Newtonsoft.Json.Converters;

namespace TelegramBot
{
    public static class TelegramData
    {
        public static Data Data;
        public static TelegramBotClient Cortana;

        public static void Init(TelegramBotClient newClient)
        {
            Cortana = newClient;
            LoadData();
        }

        public static void SendToUser(long userID, string message)
        {
            ChatId Chat = new ChatId(userID);
            Cortana.SendTextMessageAsync(Chat, message);
        }

        static public void LoadData()
        {
            Data? DataToLoad = null;
            if (System.IO.File.Exists("Data/Telegram/Data.json"))
            {
                var file = System.IO.File.ReadAllText("Data/Telegram/Data.json");

                DataToLoad = JsonConvert.DeserializeObject<Data>(file);
            }
            if (DataToLoad != null) Data = DataToLoad;
        }
    }

    public class Data
    {
        public long ChiefID { get; set; }
    }

}