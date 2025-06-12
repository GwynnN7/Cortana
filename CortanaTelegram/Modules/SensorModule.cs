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
				string light = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Light}");
				string threshold = await ApiHandler.Get($"{ERoute.Settings}/{ESettings.LightThreshold}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{light}/{threshold}");
				break;
			case "temperature":
				string temp = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Temperature}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, temp);
				break;
			case "motion":
				string motion = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Motion}");
				await cortana.AnswerCallbackQuery(callbackQuery.Id, motion);
				break;
			case "settings":
				await cortana.EditMessageText(chatId, messageId, "Sensor Settings", replyMarkup: CreateSettingsButtons());
				break;
			case "set_lightth":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetLightThreshold, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Light Threshold (0~4096)", replyMarkup: CreateCancelButton());
				break;
			case "set_motiondetection":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.SetMotionDetection, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Set Motion Detection (0:Off/1:On)", replyMarkup: CreateCancelButton());
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
			case ETelegramChatArg.SetMotionDetection:
			{
				if (int.TryParse(messageStats.FullMessage, out int code))
				{
					message = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionDetection}", new PostValue(Math.Clamp(code, 0, 1)));
				}
				else
				{
					message = "Please enter a valid number";
				}
				break;
			}
			case ETelegramChatArg.SetLightThreshold:
			{
				string lightLevel = await ApiHandler.Get($"{ERoute.Sensors}/{ESensor.Light}");
				if (int.TryParse(messageStats.FullMessage, out int threshold))
				{
					string lightThreshold = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.LightThreshold}", new PostValue(threshold));
					message = $"{lightLevel}/{lightThreshold}";
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
					message = await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MorningHour}", new PostValue(hour));
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
					message = await ApiHandler.Post($"{ERoute.Settings}/{setting}", new PostValue(motion));
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
			.AddButton("Motion Detection", "sensor-set_motiondetection")
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