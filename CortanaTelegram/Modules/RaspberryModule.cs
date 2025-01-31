using CortanaLib;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
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
		switch (command)
		{
			case "ip":
				string ip = await ApiHandler.Get("raspberry", "ip");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"IP: {ip}");
				break;
			case "gateway":
				string gateway = await ApiHandler.Get("raspberry", "gateway");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Gateway: {gateway}");
				break;
			case "location":
				string location = await ApiHandler.Get("raspberry", "location");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Location: {location}");
				break;
			case "temperature":
				string temp = await ApiHandler.Get("raspberry", "temperature");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Temperature: {temp}");
				break;
			case "reboot":
				string rebootResult = await ApiHandler.Post("reboot", "raspberry");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, rebootResult, true);
				break;
			case "shutdown":
				string shutdownResult = await ApiHandler.Post("shutdown", "raspberry");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, shutdownResult, true);
				break;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		/*
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
		
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
		*/
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
			.AddButton("<<", "home");
	}
}