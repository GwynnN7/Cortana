using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Processor;

namespace TelegramBot
{
    public static class TelegramUtils
    {
        private static readonly DataStruct Data;
        private static TelegramBotClient _cortana = null!;

        static TelegramUtils()
        {
            Data = Software.LoadFile<DataStruct>("Storage/Config/Telegram/TelegramData.json");
            Console.WriteLine($"Nums: {Data.Groups.Count} {Data.Usernames.Count} {Data.RootPermissions.Count}\n"); //-----------------------------------------------
        }
        
        public static void Init(TelegramBotClient newClient)
        {
            _cortana = newClient;
        }

        public static void SendToUser(long userId, string message, bool notify = true)
        {
            var chat = new ChatId(userId);
            _cortana.SendMessage(chat, message, disableNotification: !notify);
        }

        public static string IdToName(long id)
        {
            return Data.Usernames.GetValueOrDefault(id, "");
        }

        public static string IdToGroupName(long id)
        {
            return Data.Groups.GetValueOrDefault(id, "");
        }

        public static long NameToId(string name)
        {
            foreach ((long groupId,_) in Data.Usernames.Where(item => item.Value == name)) 
                return groupId;
            return -1;
        }

        public static long NameToGroupId(string name)
        {
            foreach ((long groupId, _) in Data.Groups.Where(item => item.Value == name)) 
                return groupId;
            return -1;
        }
        
        public static bool CheckPermission(long userId)
        {
            return true;
            return Data.RootPermissions.Contains(userId);
        }
    }

    [method: JsonConstructor]
    public readonly struct DataStruct(Dictionary<long, string> usernames, Dictionary<long, string> groups, List<long> permissions)
    {
        public Dictionary<long, string> Usernames { get; } = usernames;
        public Dictionary<long, string> Groups { get; } = groups;
        public List<long> RootPermissions { get; } = permissions;
    }
    
    public struct MessageStats
    {
        public string FullMessage;
        public string Command;
        public string Text;
        public List<string> TextList;
        public long ChatId;
        public long UserId;
        public int MessageId;
        public ChatType ChatType;
    }
}
