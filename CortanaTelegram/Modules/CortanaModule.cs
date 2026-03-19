using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal sealed class CortanaModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message? message = null)
	{
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

		string messageText = "";

		if (message != null)
		{
			await cortana.EditMessageText(message.Chat.Id, message.MessageId, messageText, replyMarkup: CreateButtons());
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Devices, replyMarkup: CreateButtons());
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup();
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup();
	}
}