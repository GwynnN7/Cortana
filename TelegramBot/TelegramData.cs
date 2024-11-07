using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Modules;

namespace TelegramBot
{
    public static class TelegramData
    {
        private static Data _data = null!;
        private static TelegramBotClient _cortana = null!;
        private static List<long> _rootPermissions = null!;
        
        public static void Init(TelegramBotClient newClient)
        {
            _cortana = newClient;
            
            LoadData();
            ShoppingModule.LoadDebts();
            
            _rootPermissions = [ NameToId("@gwynn7"), NameToId("@alessiaat1") ];
        }

        private static void LoadData()
        {
            _data = Utility.Functions.LoadFile<Data>("Config/Telegram/TelegramData.json") ?? new Data();
        }

        public static void SendToUser(long userId, string message, bool notify = true)
        {
            var chat = new ChatId(userId);
            _cortana.SendMessage(chat, message, disableNotification: !notify);
        }

        public static string IdToName(long id)
        {
            return _data.usernames.GetValueOrDefault(id, "");
        }

        public static string IdToGroupName(long id)
        {
            return _data.groups.GetValueOrDefault(id, "");
        }

        public static long NameToId(string name)
        {
            foreach ((long groupId,_) in _data.usernames.Where(item => item.Value == name)) 
                return groupId;
            return -1;
        }

        public static long NameToGroupId(string name)
        {
            foreach ((long groupId, _) in _data.groups.Where(item => item.Value == name)) 
                return groupId;
            return -1;
        }
        
        public static bool CheckPermission(long userId)
        {
            return _rootPermissions.Contains(userId);
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