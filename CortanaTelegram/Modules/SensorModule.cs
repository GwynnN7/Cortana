using CortanaLib;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal abstract class SensorModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Sensor Menu", replyMarkup: CreateButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		

		switch (command)
		{
			case "light":
				string light = await ApiHandler.Get("sensor", "light");
				string threshold = await ApiHandler.Get("sensor", "settings", "lightThreshold");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Light/Threshold: {light}/{threshold}");
				break;
			case "temperature":
				string temp = await ApiHandler.Get("sensor", "temperature");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Room Temperature: {temp}");
				break;
			case "motion":
				string motion = await ApiHandler.Get("sensor", "motion");
				string currentMode = await ApiHandler.Get("sensor", "settings", "currentControlMode");
				string limitMode = await ApiHandler.Get("sensor", "settings", "limitControlMode");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{motion} ~ Current/Limit: {currentMode}/{limitMode}");
				break;
			case "settings":
				await cortana.EditMessageText(chatId, messageId, "Sensor Settings", replyMarkup: CreateSettingsButtons());
				break;
			case "set_light_th":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetLightThreshold, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the threshold level", replyMarkup: CreateCancelButton());
				break;
			case "set_control_mode":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetControlMode, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the control mode code (1/2/3)", replyMarkup: CreateCancelButton());
				break;
			case "cancel":
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
			case ETelegramChatArg.SetControlMode:
			{
				string limitMode;
				string currentMode = await ApiHandler.Get("sensor", "settings", "currentControlMode");
				if (int.TryParse(messageStats.FullMessage, out int code))
				{
					limitMode = await ApiHandler.Post(Math.Clamp(code, 1, 3).ToString(), "sensor", "settings", "limitControlMode");// TODO: dadwa
				}
				else
				{
					break; // TODO: dawwad
				}
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				string modeResponse = $"Current: {currentMode} ~ Limit: {limitMode}";
				await TelegramUtils.AnswerOrMessage(cortana, modeResponse, messageStats.ChatId,
					TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			}
			case ETelegramChatArg.SetLightThreshold:
			{
				string lightThreshold;
				string lightLevel = await ApiHandler.Get("sensor", "light");
				if (int.TryParse(messageStats.FullMessage, out int threshold))
				{
					lightThreshold = await ApiHandler.Post(threshold.ToString(), "sensor", "settings", "lightThreshold");
				}
				else break;  // TODO: dawwaddw
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				string lightResponse = $"Current: {lightLevel} ~ Threshold: {lightThreshold}";
				await TelegramUtils.AnswerOrMessage(cortana, lightResponse, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			}
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Light", "sensor-light")
			.AddNewRow()
			.AddButton("Temperature", "sensor-temperature")
			.AddNewRow()
			.AddButton("Motion", "sensor-motion")
			.AddNewRow()
			.AddButton("Settings", "sensor-settings")
			.AddNewRow()
			.AddButton("<<", "home");
	}

	private static InlineKeyboardMarkup CreateSettingsButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Light Threshold", "sensor-set_light_th")
			.AddNewRow()
			.AddButton("Control Mode", "sensor-set_control_mode")
			.AddNewRow()
			.AddButton("<<", "sensor-cancel");
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "sensor-cancel");
	}
	
}