using CortanaLib.Structures;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Utility;

internal interface IModuleInterface
{
    private static Timer? UpdateTimer = null;
    public static abstract Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null);
    public static abstract Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command);
    public static abstract Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats);
    public static abstract InlineKeyboardMarkup CreateButtons();

    public static void DestroyUpdateTimer()
    {
        UpdateTimer?.Destroy();
        UpdateTimer = null;
    }
    public static void ResetUpdateTimer<TModule>(string timerTag, ITelegramBotClient cortana, CallbackQuery? query = null) where TModule : IModuleInterface
    {
        UpdateTimer?.Destroy();
        UpdateTimer = new Timer(timerTag, new TelegramTimerPayload<(ITelegramBotClient, CallbackQuery?)>(Utils.HomeId, (cortana, query)), async Task (object? sender) =>
        {
            if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

            try
            {
                if (timer.Payload is not TelegramTimerPayload<(ITelegramBotClient cortana, CallbackQuery? query)> payload) return;
                await TModule.CreateMenu(payload.Arg.cortana, payload.Arg.query);
            }
            catch { }
        }, ETimerType.Telegram).Set((20, 0, 0));
    }
}