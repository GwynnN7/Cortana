using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Utility;

namespace TelegramBot.Modules;

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
				string ip = HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Ip);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"IP: {ip}");
				break;
			case "gateway":
				string gateway = HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Gateway);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Gateway: {gateway}");
				break;
			case "location":
				string location = HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Location);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Location: {location}");
				break;
			case "temperature":
				string temp = HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Temperature);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Temperature: {temp}");
				break;
			case "reboot":
				string rebootResult = HardwareApi.Raspberry.Command(ERaspberryOption.Reboot);
				await cortana.AnswerCallbackQuery(callbackQuery.Id, rebootResult, true);
				break;
			case "shutdown":
				string shutdownResult = HardwareApi.Raspberry.Command(ERaspberryOption.Shutdown);
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