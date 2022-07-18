using Telegram.Bot;
using Telegram.Bot.Types;


namespace TelegramBot
{
    public static class TelegramData
    {
        public static long ChiefID = 327041645;
        public static TelegramBotClient Cortana;

        public static void Init(TelegramBotClient newClient)
        {
            Cortana = newClient;
        }

        public static void SendToUser(long userID, string message)
        {
            ChatId Chat = new ChatId(userID);
            Cortana.SendTextMessageAsync(Chat, message);
        }
    }
}
