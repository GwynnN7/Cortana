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
				ResponseMessage light = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Light}");
				ResponseMessage threshold = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LightThreshold}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Light/Threshold: {light.Response}/{threshold.Response}");
				break;
			case "temperature":
				ResponseMessage temp = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Temperature}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Room Temperature: {temp.Response}");
				break;
			case "motion":
				ResponseMessage motion = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Motion}");
				ResponseMessage currentMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.ControlMode}");
				ResponseMessage limitMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LimitMode}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{motion.Response} ~ Current/Limit: {currentMode.Response}/{limitMode.Response}");
				break;
			case "settings":
				await cortana.EditMessageText(chatId, messageId, "Sensor Settings", replyMarkup: CreateSettingsButtons());
				break;
			case "set_lightth":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetLightThreshold, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Light Threshold (0~4096)", replyMarkup: CreateCancelButton());
				break;
			case "set_controlmode":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetControlMode, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Limit Control Mode (1/2/3)", replyMarkup: CreateCancelButton());
				break;
			case "set_motionoffmax":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetMotionOffMax, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Motion-Off Maximum Time", replyMarkup: CreateCancelButton());
				break;
			case "set_motionoffmin":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetMotionOffMin, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Motion-Off Minimum Time", replyMarkup: CreateCancelButton());
				break;
			case "set_morninghour":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetMorningHour, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Morning Hour (0~23)", replyMarkup: CreateCancelButton());
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
		string message = "Unknown Message";
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.SetControlMode:
			{
				ResponseMessage currentMode = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.ControlMode}");
				if (int.TryParse(messageStats.FullMessage, out int code))
				{
					ResponseMessage limitMode = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LimitMode}", new PostValue(Math.Clamp(code, 1, 3)));
					message = $"Current/Limit: {currentMode.Response}/{limitMode.Response}";
				}
				else
				{
					message = "Please enter a valid number";
				}
				break;
			}
			case ETelegramChatArg.SetLightThreshold:
			{
				ResponseMessage lightLevel = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Light}");
				if (int.TryParse(messageStats.FullMessage, out int threshold))
				{
					ResponseMessage lightThreshold = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LightThreshold}", new PostValue(threshold));
					message = $"Current/Threshold: {lightLevel.Response}/{lightThreshold.Response}";
				}
				else
				{
					message = "Please enter a valid number";
				}
				break;
			}
			case ETelegramChatArg.SetMorningHour:
			{
				if (int.TryParse(messageStats.FullMessage, out int hour))
				{
					ResponseMessage morningHour = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MorningHour}", new PostValue(hour));
					message = $"Morning Hour: {morningHour.Response}";
				}
				else
				{
					message = "Please enter a valid number";
				}
			
				break;
			}
			case ETelegramChatArg.SetMotionOffMax:
			case ETelegramChatArg.SetMotionOffMin:
			{
				ESettings setting = TelegramUtils.ChatArgs[messageStats.ChatId].Type == ETelegramChatArg.SetMotionOffMax ? ESettings.MotionOffMax : ESettings.MotionOffMin;
				if (int.TryParse(messageStats.FullMessage, out int motion))
				{
					ResponseMessage motionOff = await ApiHandler.Post($"{ERoute.Settings}/{setting}", new PostValue(motion));
					message = $"{setting} Time: {motionOff.Response}";
				}
				else
				{
					message = "Please enter a valid number";
				}
			
				break;
			}
		}
		await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
		await TelegramUtils.AnswerOrMessage(cortana, message, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
		await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
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
			.AddButton("Light Threshold", "sensor-set_lightth")
			.AddNewRow()
			.AddButton("Control Mode", "sensor-set_controlmode")
			.AddNewRow()
			.AddButton("Morning Hour", "sensor-set_morninghour")
			.AddNewRow()
			.AddButton("MotionOff Max", "sensor-set_motionoffmax")
			.AddNewRow()
			.AddButton("MotionOff Min", "sensor-set_motionoffmin")
			.AddNewRow()
			.AddButton("<<", "sensor-cancel");
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "sensor-cancel");
	}
	
}