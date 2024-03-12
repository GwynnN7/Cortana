using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Modules;

namespace TelegramBot
{
    public static class TelegramData
    {
        public static Data Data;
        public static TelegramBotClient Cortana;
        private static List<long> RootPermissions=
        [
            NameToID("@gwynn7"),
            NameToID("@alessiaat1")
        ];
        
        public static void Init(TelegramBotClient newClient)
        {
            Cortana = newClient;
            LoadData();
            ShoppingModule.LoadDebts();
        }

        static public void LoadData()
        {
            Data = Utility.Functions.LoadFile<Data>("Data/Telegram/Data.json") ?? new();
        }

        public static void SendToUser(long userID, string message, bool notify = true)
        {
            ChatId Chat = new ChatId(userID);
            Cortana.SendTextMessageAsync(Chat, message, disableNotification: !notify);
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
        
        public static bool CheckPermission(long userId)
        {
            return RootPermissions.Contains(userId);
        }
    }

    public class Data
    {
        public Dictionary<long, string> usernames { get; }
        public Dictionary<long, string> groups { get; }
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

        public MessageStats(long chatId, long userId, int messageId, ChatType chatType)
        {
            ChatID = chatId;
            UserID = userId;
            MessageID = messageId;
            ChatType = chatType;
        }
    }
}