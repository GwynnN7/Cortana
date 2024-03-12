using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Modules;

namespace TelegramBot
{
    public static class TelegramData
    {
        private static Data Data;
        private static TelegramBotClient Cortana;
        private static List<long> RootPermissions = new() { NameToID("@gwynn7"), NameToID("@alessiaat1") };
        
        public static void Init(TelegramBotClient newClient)
        {
            Cortana = newClient;
            Console.WriteLine("addwadadawwawwa1");
            LoadData();
            Console.WriteLine("addwadwawdawa1");
            ShoppingModule.LoadDebts();
        }

        private static void LoadData()
        {
            Data = Utility.Functions.LoadFile<Data>("Data/Telegram/Data.json") ?? new();
            Console.WriteLine("adwa1");
            Console.WriteLine(Data.usernames.Count);
            Console.WriteLine("adwa2");
            Console.WriteLine(Data.groups.Count);
            Console.WriteLine("adwa3");
        }

        public static void SendToUser(long userId, string message, bool notify = true)
        {
            ChatId chat = new ChatId(userId);
            Cortana.SendTextMessageAsync(chat, message, disableNotification: !notify);
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
            Console.WriteLine("Porco dio");
            Console.WriteLine(name);
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
        
        public static bool CheckPermission(long userId)
        {
            return RootPermissions.Contains(userId);
        }
    }

    public class Data
    {
        public Dictionary<long, string> usernames { get; set; }
        public Dictionary<long, string> groups { get; set; }
    }
    
    public struct MessageStats
    {
        public string FullMessage;
        public string Command;
        public string Text;
        public List<string> TextList;
        public long ChatID;
        public long UserID;
        public int MessageID;
        public ChatType ChatType;
    }
}