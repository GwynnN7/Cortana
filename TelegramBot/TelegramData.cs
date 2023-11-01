using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;

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
            Shopping.LoadDebts();
        }

        public static void SendToUser(long userID, string message, bool notify = true)
        {
            ChatId Chat = new ChatId(userID);
            Cortana.SendTextMessageAsync(Chat, message, disableNotification: !notify);
        }

        static public void LoadData()
        {
            Data? DataToLoad = Utility.Functions.LoadFile<Data>("Data/Telegram/Data.json");
            if (DataToLoad != null) Data = DataToLoad;
        }
    }

    public class Data
    {
        public long ChiefID { get; set; }
        public long LF_ID { get; set; }
        public long PSM_ID { get; set; }
    }

}