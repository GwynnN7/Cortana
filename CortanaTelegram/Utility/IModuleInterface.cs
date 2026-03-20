using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Utility;

internal interface IModuleInterface
{
    public static abstract Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null);
    public static abstract Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command);
    public static abstract Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats);
    public static abstract InlineKeyboardMarkup CreateButtons();
}