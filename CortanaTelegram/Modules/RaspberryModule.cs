using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal abstract class RaspberryModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Raspberry Menu", replyMarkup: CreateButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		
		switch (command)
		{
			case "ip":
			case "gateway":
			case "location":
			case "temperature":
				string messageResponse = await ApiHandler.Get($"{ERoute.Raspberry}/{command}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{command.Capitalize()}: {messageResponse}");
				break;
			case "command":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<List<int>>(ETelegramChatArg.RaspberryCommand, callbackQuery, callbackQuery.Message, []), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
				break;
			case "reboot":
			case "shutdown":
				string commandMessageResponse = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(command));
				await cortana.AnswerCallbackQuery(callbackQuery.Id, commandMessageResponse, true);
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
		   	case ETelegramChatArg.RaspberryCommand:
		   		if (TelegramUtils.ChatArgs[messageStats.ChatId] is TelegramChatArg<List<int>> chatArg)
		   		{
		   			string commandResult = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand($"{EComputerCommand.Command}", string.Concat(messageStats.FullMessage[..1].ToLower(), messageStats.FullMessage.AsSpan(1))));
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
			.AddButton("Shutdown", "raspberry-shutdown")
			.AddButton("Reboot", "raspberry-reboot")
			.AddNewRow()
			.AddButton("Temperature", "raspberry-temperature")
			.AddButton("IP", "raspberry-ip")
			.AddNewRow()
			.AddButton("Location", "raspberry-location")
			.AddButton("Gateway", "raspberry-gateway")
			.AddNewRow()
			.AddButton("Command", "raspberry-command")
			.AddNewRow()
			.AddButton("<<", "home");
	}
	
	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "raspberry-cancel");
	}
}