using CortanaLib.Runtime;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// One updating message per topic
public abstract class Menu
{
	public abstract string Tag { get; }

	public abstract int Topic { get; }

	protected abstract Task<string> Render();

	protected abstract InlineKeyboardMarkup Keyboard();

	public abstract Task Handle(CallbackQuery query, string command);

	public virtual Task Handle(IncomingText message, PendingInput pending) => Task.CompletedTask;

	public async Task Show(CallbackQuery? query = null)
	{
		_ = TelegramSession.Bot.SendChatAction(TelegramSession.HomeId, ChatAction.Typing);
		TelegramSession.End(Topic);

		string text = await Render();

		if (query?.Message != null && (query.Message.MessageThreadId ?? 0) == Topic)
		{
			try
			{
				await TelegramSession.Bot.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, text,
					replyMarkup: Keyboard(), parseMode: ParseMode.Html);
			}
			catch (Exception ex) when (!ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
			{
				Log.Write("Telegram", $"Could not edit the {Tag} menu: {ex.Message}");
			}

			LiveMenu.Track(this, query.Message.MessageId, text, Keyboard());
			return;
		}

		Message sent = await TelegramSession.Post(text, Topic, ParseMode.Html, Keyboard());
		LiveMenu.Track(this, sent.MessageId, text, Keyboard());
	}

	internal Task<string> RenderForRefresh() => Render();

	internal InlineKeyboardMarkup KeyboardForRefresh() => Keyboard();

	protected static InlineKeyboardMarkup Buttons() => new();
}
