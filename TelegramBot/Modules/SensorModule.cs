using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Kernel.Hardware.Interfaces;
using TelegramBot.Utility;

namespace TelegramBot.Modules;

internal static class SensorModule
{
	public static async Task CreateSensorMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Sensor Handler", replyMarkup: CreateSensorButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		
		if (command.StartsWith("sensor-"))
		{
			switch (command["sensor-".Length..])
			{
				case "light":
					string light = HardwareProxy.GetSensorInfo(ESensor.Light);
					int threshold = HardwareSettings.LightThreshold;
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Light Level: {light} ~ Threshold: {threshold}");
					break;
				case "temperature":
					string temp = HardwareProxy.GetSensorInfo(ESensor.Temperature);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Room Temperature: {temp}");
					break;
				case "motion":
					string motion = HardwareProxy.GetSensorInfo(ESensor.Motion);
					EControlMode currentMode = HardwareSettings.CurrentControlMode;
					EControlMode limitMode = HardwareSettings.LimitControlMode;
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"{motion} ~ Current/Limit Mode: {currentMode}/{limitMode}");
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
						await cortana.EditMessageText(chatId, messageId, "Write the control mode code (0/1/2)", replyMarkup: CreateCancelButton());
					break;
				case "cancel":
					await CreateSensorMenu(cortana, callbackQuery.Message);
					TelegramUtils.ChatArgs.Remove(chatId);
					break;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
		
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.SetControlMode:
				if (int.TryParse(messageStats.FullMessage, out int code))
				{
					EControlMode mode = code switch
					{
						0 => EControlMode.Manual,
						1 => EControlMode.NightHandler,
						2 => EControlMode.MotionSensor,
						_ => HardwareSettings.LimitControlMode
					};
					HardwareSettings.LimitControlMode = mode;
				}
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);

				string modeResponse = $"Limit mode: {HardwareSettings.LimitControlMode} ~ Current mode: {HardwareSettings.CurrentControlMode}";
				await TelegramUtils.AnswerOrMessage(cortana, modeResponse, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateSensorMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.SetLightThreshold:
				if (int.TryParse(messageStats.FullMessage, out int threshold))
				{
					HardwareSettings.LightThreshold = threshold;
				}
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				string lightResponse = $"Current Light: {HardwareProxy.GetSensorInfo(ESensor.Light)} ~ Light Threshold: {HardwareSettings.LightThreshold}";
				await TelegramUtils.AnswerOrMessage(cortana, lightResponse, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateSensorMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
	}

	private static InlineKeyboardMarkup CreateSensorButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Light", "sensor-light")
			.AddNewRow()
			.AddButton("Temperature", "sensor-temperature")
			.AddNewRow()
			.AddButton("Motion", "sensor-motion")
			.AddNewRow()
			.AddButton("Settings", "sensor-settings");
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