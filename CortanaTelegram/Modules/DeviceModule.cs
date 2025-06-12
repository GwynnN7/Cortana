using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal abstract class DeviceModule : IModuleInterface
{
	private static readonly Dictionary<long, string> HardwareAction = new();

	public static async Task ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
	{
		switch (messageStats.Command)
		{
			case "domotica":
				if (TelegramUtils.CheckHardwarePermission(messageStats.UserId))
					await cortana.SendMessage(messageStats.ChatId, "Keyboard Domotica", replyMarkup: CreateHardwareToggles());
				else await cortana.SendMessage(messageStats.ChatId, "Sorry, you can't use this command");
				break;
		}
	}

	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		HardwareAction.Remove(message.Id);
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Device Menu", replyMarkup: CreateButtons());
	}
	
	public static async Task<bool> HandleKeyboardCallback(ITelegramBotClient cortana, MessageStats messageStats)
	{
		if (!TelegramUtils.CheckHardwarePermission(messageStats.UserId) || messageStats.ChatType != ChatType.Private) return false;
		string arg = messageStats.FullMessage;
		string? response = arg switch
		{
			HardwareEmoji.Lamp => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Lamp}"),
			HardwareEmoji.Pc => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Computer}"),
			HardwareEmoji.Generic => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Generic}"),
			HardwareEmoji.On => await ApiHandler.Post($"{ERoute.Devices}/room", new PostAction($"{EPowerAction.On}")),
			HardwareEmoji.Off => await ApiHandler.Post($"{ERoute.Devices}/room", new PostAction($"{EPowerAction.Off}")),
			HardwareEmoji.Night => await ApiHandler.Post($"{ERoute.Devices}/sleep"),
			HardwareEmoji.Reboot => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.Reboot}")),
			HardwareEmoji.System => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.System}")),
			_ => null
		};
		if (response == null) return false;
		await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
		return true;
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		
	
		if (!HardwareAction.TryAdd(messageId, command))
		{
			switch (command)
			{
				case "timer":
					await cortana.EditMessageText(chatId, messageId, "Select the action of the timer", replyMarkup: CreateOnOffTimerButtons());
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "Timer pattern: {sec}s {min}m {hours}h {days}d");
					break;
				case var _ when command.StartsWith("timer-"):
					command = command["timer-".Length..];
					if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<string>(ETelegramChatArg.HardwareTimer, callbackQuery, callbackQuery.Message, command), callbackQuery))
						await cortana.EditMessageText(chatId, messageId, "Set the timer for the action", replyMarkup: CreateCancelButton());
					break;
				case "cancel":
					await CreateMenu(cortana, TelegramUtils.ChatArgs[chatId].InteractionMessage);
					TelegramUtils.ChatArgs.Remove(chatId);
					break;
				default:
					string result = await ApiHandler.Post($"{ERoute.Devices}/{HardwareAction[messageId]}", new PostAction(command));
					await cortana.AnswerCallbackQuery(callbackQuery.Id, result);
					await CreateMenu(cortana, callbackQuery.Message);
					break;
			}
			return;
		}
		string devicePower = await ApiHandler.Get($"{ERoute.Devices}/{command}");
		await cortana.EditMessageText(callbackQuery.Message.Chat.Id, messageId, devicePower, replyMarkup: CreateOnOffToggleButtons());
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
		
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.HardwareTimer:
				(int s, int m, int h, int d) times;
				try
				{
					times = TelegramUtils.ParseTime(messageStats.FullMessage);
				}
				catch
				{
					await TelegramUtils.AnswerOrMessage(cortana, "Time pattern is incorrect, try again!", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
					return;
				}
		
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				(string, string) hardwarePattern = (HardwareAction[TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage.MessageId], (TelegramUtils.ChatArgs[messageStats.ChatId] as TelegramChatArg<string>)!.Arg);
				var timer = new Timer($"{messageStats.UserId}:{DateTime.UnixEpoch.Second}", new TelegramTimerPayload<(string, string)>(messageStats.ChatId, messageStats.UserId, hardwarePattern), HardwareTimerFinished, ETimerType.Telegram);
				timer.Set((times.s, times.m, times.h));
				
				await TelegramUtils.AnswerOrMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
	}
	
	private static async Task HardwareTimerFinished(object? sender)
	{
		if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

		try
		{
			if (timer.Payload is not TelegramTimerPayload<(string device, string action)> payload) return;
			string result = await ApiHandler.Post($"{ERoute.Devices}/{payload.Arg.device}", new PostAction(payload.Arg.action));
			await TelegramUtils.SendToUser(payload.UserId, $"Timer elapsed with result: {result}");
		}
		catch (Exception e)
		{
			await TelegramUtils.SendToUser(TelegramUtils.AuthorId, $"There was an error with a timer:\n```{e.Message}```");
		}
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup()
			.AddButton("Room", "device-room")
			.AddNewRow();

		foreach (string element in Enum.GetNames<EDevice>())
		{
			inlineKeyboard.AddButton(element, $"device-{element.ToLower()}");
			inlineKeyboard.AddNewRow();
		}

		inlineKeyboard.AddButton("<<", "home");
		return inlineKeyboard;
	}

	private static InlineKeyboardMarkup CreateOnOffToggleButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On", "device-on")
			.AddButton("Off", "device-off")
			.AddNewRow()
			.AddButton("Toggle", "device-toggle")
			.AddNewRow()
			.AddButton("Timer", "device-timer")
			.AddNewRow()
			.AddButton("<<", "device");
	}
	
	private static InlineKeyboardMarkup CreateOnOffTimerButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On", "device-timer-on")
			.AddButton("Off", "device-timer-off")
			.AddNewRow()
			.AddButton("<<", "device");
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "device-cancel");
	}

	private static ReplyKeyboardMarkup CreateHardwareToggles()
	{
		return new ReplyKeyboardMarkup(true)
			.AddButtons(HardwareEmoji.Lamp, HardwareEmoji.Generic)
			.AddNewRow()
			.AddButtons(HardwareEmoji.Pc, HardwareEmoji.Reboot, HardwareEmoji.System)
			.AddNewRow()
			.AddButtons(HardwareEmoji.On, HardwareEmoji.Night, HardwareEmoji.Off);
	}
}