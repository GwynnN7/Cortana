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
            var DataToLoad = Utility.Functions.LoadFile<Data>("Data/Telegram/Data.json");
            if (DataToLoad != null) Data = DataToLoad;
        }

        public static string IDToName(long id)
        {
            if (!Data.usernames.ContainsKey(id)) return "";
            return Data.usernames[id];
        }

        public static string IDToGroupName(long id)
        {
            if (!Data.groups.ContainsKey(id)) return "";
            return Data.groups[id];
        }

        public static long NameToID(string name)
        {
            foreach (var item in Data.usernames)
            {
                if (item.Value == name) return item.Key;
            }
            return -1;
        }

        public static long NameToGroupID(string name)
        {
            foreach (var item in Data.groups)
            {
                if (item.Value == name) return item.Key;
            }
            return -1;
        }
    }

    public class Data
    {
        public Dictionary<long, string> usernames { get; set; }
        public Dictionary<long, string> groups { get; set; }
    }
}