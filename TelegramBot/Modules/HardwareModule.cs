using System.Timers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.Utility;
using TelegramBot.Utility;
using Enum = System.Enum;
using Timer = Kernel.Software.Timer;

namespace TelegramBot.Modules;

internal static class HardwareModule
{
	private static readonly Dictionary<long, string> HardwareAction = new();

	public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
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

	public static async void CreateAutomationMenu(ITelegramBotClient cortana, Message message)
	{
		HardwareAction.Remove(message.Id);
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Hardware Keyboard", replyMarkup: CreateAutomationButtons());
	}

	public static async void CreateRaspberryMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Raspberry Handler", replyMarkup: CreateRaspberryButtons());
	}

	public static async void CreateHardwareUtilityMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Hardware Utility", replyMarkup: CreateUtilityButtons());
	}

	public static async Task<bool> HandleKeyboardCallback(ITelegramBotClient cortana, MessageStats messageStats)
	{
		if (!TelegramUtils.CheckHardwarePermission(messageStats.UserId) || messageStats.ChatType != ChatType.Private) return false;
		switch (messageStats.FullMessage)
		{
			case HardwareEmoji.Lamp:
				HardwareProxy.SwitchDevice(EDevice.Lamp, EPowerAction.Toggle);
				break;
			case HardwareEmoji.Pc:
				HardwareProxy.SwitchDevice(EDevice.Computer, EPowerAction.Toggle);
				break;
			case HardwareEmoji.Generic:
				HardwareProxy.SwitchDevice(EDevice.Generic, EPowerAction.Toggle);
				break;
			case HardwareEmoji.On:
				HardwareProxy.SwitchRoom(EPowerAction.On);
				break;
			case HardwareEmoji.Off:
				HardwareProxy.SwitchRoom(EPowerAction.Off);
				break;
			case HardwareEmoji.Reboot:
				HardwareProxy.CommandComputer(EComputerCommand.Reboot);
				break;
			case HardwareEmoji.SwapOs:
				HardwareProxy.CommandComputer(EComputerCommand.SwapOs);
				break;
			default:
				return false;
		}

		await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
		return true;
	}

	public static async void HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;
		
		if (command.StartsWith("raspberry-"))
		{
			switch (command["raspberry-".Length..])
			{
				case "ip":
					string ip = HardwareProxy.GetHardwareInfo(EHardwareInfo.Ip);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"IP: {ip}");
					break;
				case "gateway":
					string gateway = HardwareProxy.GetHardwareInfo(EHardwareInfo.Gateway);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Gateway: {gateway}");
					break;
				case "location":
					string location = HardwareProxy.GetHardwareInfo(EHardwareInfo.Location);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Location: {location}");
					break;
				case "temperature":
					string temp = HardwareProxy.GetHardwareInfo(EHardwareInfo.Temperature);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Temperature: {temp}");
					break;
				case "reboot":
					string rebootResult = HardwareProxy.CommandRaspberry(ERaspberryOption.Reboot);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, rebootResult, true);
					break;
				case "shutdown":
					string shutdownResult = HardwareProxy.CommandRaspberry(ERaspberryOption.Shutdown);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, shutdownResult, true);
					break;
			}
		}
		else if (command.StartsWith("automation-"))
		{
			command = command["automation-".Length..];

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
							await cortana.EditMessageText(chatId, messageId, "Set the timer for the action", replyMarkup: CreateCancelButton("automation"));
						break;
					case "cancel":
						CreateAutomationMenu(cortana, TelegramUtils.ChatArgs[chatId].InteractionMessage);
						TelegramUtils.ChatArgs.Remove(chatId);
						break;
					default:
						string result = HardwareProxy.SwitchDevice(HardwareAction[messageId], command);
						await cortana.AnswerCallbackQuery(callbackQuery.Id, result);
						CreateAutomationMenu(cortana, callbackQuery.Message);
						break;
				}
				return;
			}
			string devicePower = HardwareProxy.GetDevicePower(command);
			await cortana.EditMessageText(callbackQuery.Message.Chat.Id, messageId, devicePower, replyMarkup: CreateOnOffToggleButtons());
		}
		else if (command.StartsWith("utility-"))
		{
			switch (command["utility-".Length..])
			{
				case "notify":
					if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Notification, callbackQuery, callbackQuery.Message), callbackQuery))
						await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton("utility"));
					break;
				case "ping":
					if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Ping, callbackQuery, callbackQuery.Message), callbackQuery))
						await cortana.EditMessageText(chatId, messageId, "Write the IP of the host you want to ping", replyMarkup: CreateCancelButton("utility"));
					break;
				case "swap_os":
					string result = HardwareProxy.CommandComputer(EComputerCommand.SwapOs);
					await cortana.AnswerCallbackQuery(callbackQuery.Id, result);
					break;
				case "command":
					if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<List<int>>(ETelegramChatArg.ComputerCommand, callbackQuery, callbackQuery.Message, []), callbackQuery))
						await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton("utility"));
					break;
				case "cancel":
					if (TelegramUtils.ChatArgs.TryGetValue(chatId, out TelegramChatArg? value) && value is TelegramChatArg<List<int>> chatArg)
						await cortana.DeleteMessages(chatId, chatArg.Arg);
					CreateHardwareUtilityMenu(cortana, callbackQuery.Message);
					TelegramUtils.ChatArgs.Remove(chatId);
					break;
			}
		}
	}

	public static async void HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
		
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.Notification:
				string result = HardwareProxy.CommandComputer(EComputerCommand.Notify, messageStats.FullMessage);
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				TelegramUtils.AnswerOrMessage(cortana, result, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				CreateHardwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.Ping:
				string pingResult = HardwareProxy.Ping(messageStats.FullMessage) ? "Host reached successfully!" : "Host could not be reached!";
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				TelegramUtils.AnswerOrMessage(cortana, pingResult, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
				CreateHardwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.ComputerCommand:
				if (TelegramUtils.ChatArgs[messageStats.ChatId] is TelegramChatArg<List<int>> chatArg)
				{
					string commandResult = HardwareProxy.CommandComputer(EComputerCommand.Command, string.Concat(messageStats.FullMessage[..1].ToLower(), messageStats.FullMessage.AsSpan(1)));
					Message msg = await cortana.SendMessage(messageStats.ChatId, commandResult);
					chatArg.Arg.Add(messageStats.MessageId);
					chatArg.Arg.Add(msg.MessageId);
					return;
				}
				CreateHardwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
			case ETelegramChatArg.HardwareTimer:
				(int s, int m, int h, int d) times;
				try
				{
					times = TelegramUtils.ParseTime(messageStats.FullMessage);
				}
				catch
				{
					TelegramUtils.AnswerOrMessage(cortana, "Time pattern is incorrect, try again!", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
					return;
				}
		
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				(string, string) hardwarePattern = (HardwareAction[TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage.MessageId], (TelegramUtils.ChatArgs[messageStats.ChatId] as TelegramChatArg<string>)!.Arg);
				var timer = new Timer($"{messageStats.UserId}:{DateTime.UnixEpoch.Second}", new TelegramTimerPayload<(string, string)>(messageStats.ChatId, messageStats.UserId, hardwarePattern), 
					(times.s, times.m, times.h), HardwareTimerFinished, ETimerType.Telegram);
				
				TelegramUtils.AnswerOrMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				CreateAutomationMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				break;
		}
		TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
	}
	
	private static void HardwareTimerFinished(object? sender, ElapsedEventArgs args)
	{
		if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

		try
		{
			if (timer.Payload is not TelegramTimerPayload<(string device, string action)> payload) return;
			string result = HardwareProxy.SwitchDevice(payload.Arg.device, payload.Arg.action);
			TelegramUtils.SendToUser(payload.UserId, $"Timer elapsed with result: {result}");
		}
		catch (Exception e)
		{
			TelegramUtils.SendToUser(TelegramUtils.AuthorId, $"There was an error with a timer:\n```{e.Message}```");
		}
	}

	private static InlineKeyboardMarkup CreateAutomationButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup()
			.AddButton("Room", "hardware-automation-room")
			.AddNewRow();

		foreach (string element in Enum.GetNames<EDevice>())
		{
			inlineKeyboard.AddButton(element, $"hardware-automation-{element.ToLower()}");
			inlineKeyboard.AddNewRow();
		}

		inlineKeyboard.AddButton("<<", "home");
		return inlineKeyboard;
	}

	private static InlineKeyboardMarkup CreateRaspberryButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Shutdown", "hardware-raspberry-shutdown")
			.AddButton("Reboot", "hardware-raspberry-reboot")
			.AddNewRow()
			.AddButton("Temperature", "hardware-raspberry-temperature")
			.AddButton("IP", "hardware-raspberry-ip")
			.AddNewRow()
			.AddButton("Location", "hardware-raspberry-location")
			.AddButton("Gateway", "hardware-raspberry-gateway")
			.AddNewRow()
			.AddButton("<<", "home");
	}

	private static InlineKeyboardMarkup CreateUtilityButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Ping", "hardware-utility-ping")
			.AddNewRow()
			.AddButton("Desktop Notification", "hardware-utility-notify")
			.AddNewRow()
			.AddButton("Swap OS", "hardware-utility-swap_os")
			.AddNewRow()
			.AddButton("Command PC", "hardware-utility-command")
			.AddNewRow()
			.AddButton("<<", "home");
	}

	private static InlineKeyboardMarkup CreateOnOffToggleButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On", "hardware-automation-on")
			.AddButton("Off", "hardware-automation-off")
			.AddNewRow()
			.AddButton("Toggle", "hardware-automation-toggle")
			.AddNewRow()
			.AddButton("Timer", "hardware-automation-timer")
			.AddNewRow()
			.AddButton("<<", "automation");
	}
	
	private static InlineKeyboardMarkup CreateOnOffTimerButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On", "hardware-automation-timer-on")
			.AddButton("Off", "hardware-automation-timer-off")
			.AddNewRow()
			.AddButton("<<", "automation");
	}

	private static InlineKeyboardMarkup CreateCancelButton(string path)
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", $"hardware-{path}-cancel");
	}

	private static ReplyKeyboardMarkup CreateHardwareToggles()
	{
		return new ReplyKeyboardMarkup(true)
			.AddButtons(HardwareEmoji.Lamp, HardwareEmoji.Generic)
			.AddNewRow()
			.AddButtons(HardwareEmoji.Pc, HardwareEmoji.Reboot, HardwareEmoji.SwapOs)
			.AddNewRow()
			.AddButtons(HardwareEmoji.On, HardwareEmoji.Off);
	}
}