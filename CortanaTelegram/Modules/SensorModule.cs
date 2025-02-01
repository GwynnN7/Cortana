using CortanaLib;
using CortanaLib.Structures;
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
				ResponseMessage light = await ApiHandler.Get($"{ERoute.Sensor}/{ESensor.Light}");
				ResponseMessage threshold = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LightThreshold}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Light/Threshold: {light.Message}/{threshold.Message}");
				break;
			case "temperature":
				ResponseMessage temp = await ApiHandler.Get($"{ERoute.Sensor}/{ESensor.Temperature}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Room Temperature: {temp.Message}");
				break;
			case "motion":
				ResponseMessage motion = await ApiHandler.Get($"{ERoute.Sensor}/{ESensor.Motion}");
				ResponseMessage currentMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.ControlMode}");
				ResponseMessage limitMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LimitMode}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{motion.Message} ~ Current/Limit: {currentMode.Message}/{limitMode.Message}");
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
				string modeResponse;
				ResponseMessage currentMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.ControlMode}");
				if (int.TryParse(messageStats.FullMessage, out int code))
				{
					ResponseMessage limitMode = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LimitMode}", new PostValue(Math.Clamp(code, 1, 3)));
					modeResponse = $"Current/Limit: {currentMode.Message}/{limitMode.Message}";
				}
				else
				{
					modeResponse = "Please enter a valid number";
				}
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				await TelegramUtils.AnswerOrMessage(cortana, modeResponse, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			}
			case ETelegramChatArg.SetLightThreshold:
			{
				string lightResponse;
				ResponseMessage lightLevel = await ApiHandler.Get($"{ERoute.Sensor}/{ESensor.Light}");
				if (int.TryParse(messageStats.FullMessage, out int threshold))
				{
					ResponseMessage lightThreshold = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LightThreshold}", new PostValue(threshold));
					lightResponse = $"Current/Threshold: {lightLevel.Message}/{lightThreshold.Message}";
				}
				else
				{
					lightResponse = "Please enter a valid number";
				}
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
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