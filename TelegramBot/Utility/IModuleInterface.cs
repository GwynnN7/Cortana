using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot.Utility;

internal interface IModuleInterface
{
    public static abstract Task CreateMenu(ITelegramBotClient cortana, Message message);
    public static abstract Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command);
    public static abstract Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats);
    public static abstract InlineKeyboardMarkup CreateButtons();
}