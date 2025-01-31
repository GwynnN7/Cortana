using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Utility;

namespace CortanaTelegram.Modules;

internal abstract class CommandModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Command Menu", replyMarkup: CreateButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		
		
		switch (command)
		{
			case "reboot":
			case "suspend":
			case "swapos":
				string result = await ApiHandler.Post(null, "computer", command);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, result);
				break;
			case "notify":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Notification, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton());
				break;
			case "command":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<List<int>>(ETelegramChatArg.ComputerCommand, callbackQuery, callbackQuery.Message, []), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
				break;
			case "ping":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Ping, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the IP of the host you want to ping", replyMarkup: CreateCancelButton());
				break;
			case "sleep":
				string sleep = await ApiHandler.Post(null, "device", "sleep");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, sleep);
				break;
			case "cancel":
				if (TelegramUtils.ChatArgs.TryGetValue(chatId, out TelegramChatArg? value) && value is TelegramChatArg<List<int>> chatArg)
					await cortana.DeleteMessages(chatId, chatArg.Arg);
				await CreateMenu(cortana, callbackQuery.Message);
				TelegramUtils.ChatArgs.Remove(chatId);
				break;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
		
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.Notification:
				string result = await ApiHandler.Post(messageStats.FullMessage, "device", "notify");
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				await TelegramUtils.AnswerOrMessage(cortana, result, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.Ping:
				// TODO string pingResult = HardwareApi.Ping(messageStats.FullMessage) ? "Host reached successfully!" : "Host could not be reached!";
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				await TelegramUtils.AnswerOrMessage(cortana, "todo", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.ComputerCommand:
				if (TelegramUtils.ChatArgs[messageStats.ChatId] is TelegramChatArg<List<int>> chatArg)
				{
					string commandResult = await ApiHandler.Post(string.Concat(messageStats.FullMessage[..1].ToLower(), messageStats.FullMessage.AsSpan(1)), "computer", "command");
					Message msg = await cortana.SendMessage(messageStats.ChatId, commandResult);
					chatArg.Arg.Add(messageStats.MessageId);
					chatArg.Arg.Add(msg.MessageId);
					return;
				}
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Reboot", "command-reboot")
			.AddButton("Suspend", "command-suspend")
			.AddNewRow()
			.AddButton("Swap OS", "command-swapos")
			.AddButton("Notify", "command-notify")
			.AddNewRow()
			.AddButton("Command PC", "command-command")
			.AddNewRow()
			.AddButton("Ping", "command-ping")
			.AddNewRow()
			.AddButton("Sleep Mode", "command-sleep")
			.AddNewRow()
			.AddButton("<<", "home");
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "command-cancel");
	}
}