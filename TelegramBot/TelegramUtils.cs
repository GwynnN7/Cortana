using System.Text.Json.Serialization;
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
        public static readonly Dictionary<long, TelegramChatArg> ChatArgs;

        static TelegramUtils()
        {
            Data = Software.LoadFile<DataStruct>("Storage/Config/Telegram/TelegramData.json");
            ChatArgs = new Dictionary<long, TelegramChatArg>();
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
            if (Data.Usernames.TryGetValue(id, out string? name))
                return name;
            throw new CortanaException("User not found");
        }

        public static string IdToGroupName(long id)
        {
            if (Data.Groups.TryGetValue(id, out string? name))
                return name;
            throw new CortanaException("Group not found");
        }

        public static long NameToId(string name)
        {
            foreach ((long groupId,_) in Data.Usernames.Where(item => item.Value == name)) 
                return groupId;
            throw new CortanaException("User not found");
        }

        public static long NameToGroupId(string name)
        {
            foreach ((long groupId, _) in Data.Groups.Where(item => item.Value == name)) 
                return groupId;
            throw new CortanaException("Group not found");
        }
        
        public static bool CheckPermission(long userId)
        {
            return Data.RootPermissions.Contains(userId);
        }

        public static bool TryAddChatArg(long chatId, TelegramChatArg arg, CallbackQuery callbackQuery)
        {
            if(ChatArgs.TryAdd(chatId, arg)) return true;
            _cortana.AnswerCallbackQuery(callbackQuery.Id, "You already have an interaction going on! Finish it before continuing", true);
            return false;
        }
        
        public static async void AnswerOrMessage(ITelegramBotClient cortana, string text, long chatId, CallbackQuery? callbackQuery)
        {
            try { await cortana.AnswerCallbackQuery(callbackQuery!.Id, text, true); }
            catch { await cortana.SendMessage(chatId, text); }
        }
    }

    [method: Newtonsoft.Json.JsonConstructor]
    public readonly struct DataStruct(
        Dictionary<long, string> usernames, 
        Dictionary<long, string> groups, 
        List<long> rootPermissions)
    {
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<long, string> Usernames { get; } = usernames;
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public Dictionary<long, string> Groups { get; } = groups;
        [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
        public List<long> RootPermissions { get; } = rootPermissions;
    }
    
    public readonly struct TelegramChatArg(ETelegramChatArg type, CallbackQuery callbackQuery, Message interactionMessage, object? arg = null)
    {
        public readonly CallbackQuery CallbackQuery = callbackQuery;
        public readonly Message InteractionMessage = interactionMessage;
        public readonly ETelegramChatArg Type = type;
        public string ArgString => (string)arg!;
        public long ArgLong => (long)arg!;
        public bool HasArg => arg != null;

    }
    
    public struct MessageStats
    {
        public Message Message;
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
