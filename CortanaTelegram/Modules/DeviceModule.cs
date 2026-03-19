using System.Collections.Concurrent;
using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal sealed class DeviceModule : IModuleInterface
{
	private static readonly ConcurrentDictionary<long, string> HardwareAction = new();
	private static int TabIndex = 0;
	private static bool TimerActive = false;

	public static async Task ExecCommand(MessageData messageStats, ITelegramBotClient cortana)
	{
		switch (messageStats.Command)
		{
			case "domotica":
				await cortana.SendMessage(messageStats.ChatId, "Keyboard Domotica", replyMarkup: CreateHardwareToggles());
				break;
		}
	}

	public static async Task CreateMenu(ITelegramBotClient cortana, Message? message = null)
	{
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

		string messageText = await GetDevicesStatus();

		if (message != null)
		{
			await cortana.EditMessageText(message.Chat.Id, message.MessageId, messageText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Devices, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
	}

	public static async Task HandleKeyboardCallback(ITelegramBotClient cortana, MessageData messageStats)
	{
		string? response = messageStats.Message switch
		{
			HardwareEmoji.Lamp => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Lamp}"),
			HardwareEmoji.Pc => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Computer}"),
			HardwareEmoji.Generic => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Generic}"),
			HardwareEmoji.On => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionDetection}", new PostValue((int)EMotionDetection.On)),
			HardwareEmoji.Off => await ApiHandler.Post($"{ERoute.Settings}/{ESettings.MotionDetection}", new PostValue((int)EMotionDetection.Off)),
			HardwareEmoji.Night => await ApiHandler.Post($"{ERoute.Devices}/sleep"),
			HardwareEmoji.Reboot => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.Reboot}")),
			HardwareEmoji.System => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.System}")),
			_ => null
		};
		await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh => CreateMenu(cortana, query.Message),
			ActionTag.Tab => Task.Run(async () =>
			{
				TabIndex = (TabIndex + 1) % 2;
				await CreateMenu(cortana, query.Message);
			}),
			ActionTag.Timer => Task.Run(async () =>
			{
				TimerActive = !TimerActive;
				await cortana.EditMessageReplyMarkup(chatId, messageId, CreateOnOffToggleButtons());
			}),
			_ => null

		};

		if (task != null)
		{
			await task;
			return;
		}

		string? response = command switch
		{
			ActionTag.System => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand("system")),
			ActionTag.Reboot => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand("reboot")),
			ActionTag.Suspend => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand("suspend")),
			ActionTag.Sleep => await ApiHandler.Post($"{ERoute.Devices}/sleep"),
			_ => null
		};

		if (response != null)
		{
			await cortana.AnswerCallbackQuery(query.Id, response);
		}
		else
		{
			switch (command)
			{
				case ActionTag.Command:
					if (Utils.AddChatArg(chatId, new ChatArgs<List<int>>(EArgsType.ComputerCommand, query, query.Message, []), query))
					{
						await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
					}
					break;
				case ActionTag.Notify:
					if (Utils.AddChatArg(chatId, new ChatArgs(EArgsType.Notification, query, query.Message), query))
					{
						await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton());
					}
					break;
				case ActionTag.Cancel:
					if (Utils.ChatArgs.TryGetValue(chatId, out ChatArgs? value) && value is ChatArgs<List<int>> chatArg)
					{
						await cortana.DeleteMessages(chatId, chatArg.Arg);
					}
					await CreateMenu(cortana, query.Message);
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
				case ActionTag.On:
				case ActionTag.Off:
				case ActionTag.Toggle:
					string action = command.Split('-').Last();
					if (TimerActive)
					{
						if (Utils.AddChatArg(chatId, new ChatArgs<string>(EArgsType.HardwareTimer, query, query.Message, action), query))
						{
							await cortana.EditMessageText(chatId, messageId, "Timer pattern: {sec}s {min}m {hours}h {days}d", replyMarkup: CreateCancelButton());
						}
					}
					else
					{
						HardwareAction.TryRemove(messageId, out string? device);
						string result = await ApiHandler.Post($"{ERoute.Devices}/{device}", new PostAction(action));
						await cortana.AnswerCallbackQuery(query.Id, result);
						await CreateMenu(cortana, query.Message);
					}

					break;
				case var _ when command.StartsWith(ActionTag.Type):
					string deviceType = command.Split('-').Last();
					HardwareAction[messageId] = deviceType;
					TimerActive = false;
					await cortana.EditMessageReplyMarkup(chatId, messageId, CreateOnOffToggleButtons());
					return;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData)
	{
		await cortana.SendChatAction(msgData.ChatId, ChatAction.Typing);

		switch (Utils.ChatArgs[msgData.ChatId].Type)
		{
			case EArgsType.HardwareTimer:
				(int s, int m, int h, int d) times;
				try
				{
					times = Utils.ParseTime(msgData.Message);
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Time pattern is incorrect, try again!", Utils.Topics.Devices, Utils.ChatArgs[msgData.ChatId].Query, false);
					return;
				}

				await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);

				HardwareAction.TryRemove(Utils.ChatArgs[msgData.ChatId].Message.MessageId, out string? device);
				(string, string) hardwarePattern = (device!, (Utils.ChatArgs[msgData.ChatId] as ChatArgs<string>)!.Arg);
				var timer = new Timer($"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", new TelegramTimerPayload<(string, string)>(msgData.ChatId, hardwarePattern), HardwareTimerFinished, ETimerType.Telegram);
				timer.Set((times.s, times.m, times.h));

				await Utils.AnswerMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", Utils.Topics.Devices, Utils.ChatArgs[msgData.ChatId].Query, false);
				break;

			case EArgsType.Notification:
				string result = await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.Notify}", msgData.Message));
				await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);

				await Utils.AnswerMessage(cortana, result, Utils.Topics.Devices, Utils.ChatArgs[msgData.ChatId].Query, false);
				break;

			case EArgsType.ComputerCommand:
				if (Utils.ChatArgs[msgData.ChatId] is ChatArgs<List<int>> chatArg)
				{
					string prompt = string.Concat(msgData.Message[..1].ToLower(), msgData.Message.AsSpan(1));
					string commandResult = await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.Command}", prompt));
					Message msg = await Utils.SendToTopic(commandResult, Utils.Topics.Devices);
					chatArg.Arg.Add(msgData.MessageId);
					chatArg.Arg.Add(msg.MessageId);
					return;
				}
				break;
		}

		Utils.ChatArgs.TryRemove(msgData.ChatId, out _);
		await CreateMenu(cortana, Utils.ChatArgs[msgData.ChatId].Message);
	}

	private static async Task HardwareTimerFinished(object? sender)
	{
		if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

		try
		{
			if (timer.Payload is not TelegramTimerPayload<(string device, string action)> payload) return;
			string result = await ApiHandler.Post($"{ERoute.Devices}/{payload.Arg.device}", new PostAction(payload.Arg.action));
			await Utils.SendToTopic($"Timer elapsed with result: {result}", Utils.Topics.Devices);
		}
		catch (Exception e)
		{
			await Utils.SendToTopic($"There was an error with a timer:\n```{e.Message}```", Utils.Topics.Devices);
		}
	}

	private static async Task<string> GetDevicesStatus()
	{
		string lamp = (await ApiHandler.Get($"{ERoute.Devices}/{EDevice.Lamp}")).Contains("On") ? "<b>ON</b> 🟢" : "<b>OFF</b> 🔴";
		string computer = (await ApiHandler.Get($"{ERoute.Devices}/{EDevice.Computer}")).Contains("On") ? "<b>ON</b> 🟢" : "<b>OFF</b> 🔴";
		string power = (await ApiHandler.Get($"{ERoute.Devices}/{EDevice.Power}")).Contains("On") ? "<b>ON</b> 🟢" : "<b>OFF</b> 🔴";
		string generic = (await ApiHandler.Get($"{ERoute.Devices}/{EDevice.Generic}")).Contains("On") ? "<b>ON</b> 🟢" : "<b>OFF</b> 🔴";

		return $"🏠 <b>Devices Status</b>\n\n• 💡 <b>Lamp</b>: {lamp}\n• 💻 <b>Computer</b>: {computer}\n• ⚡ <b>Power</b>: {power}\n• 🔌 <b>Generic</b>: {generic}";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		switch (TabIndex)
		{
			case 0:
				foreach (string element in Enum.GetNames<EDevice>())
				{
					inlineKeyboard.AddButton($"{DeviceToEmoji[element]} {element}", $"{ActionTag.Type}-{element.ToLower()}");
					inlineKeyboard.AddNewRow();
				}

				break;
			case 1:
				inlineKeyboard.AddButton("Reboot 🔄", $"{ActionTag.Reboot}");
				inlineKeyboard.AddButton("System 🎮", $"{ActionTag.System}");
				inlineKeyboard.AddNewRow();
				inlineKeyboard.AddButton("Suspend 🌙", $"{ActionTag.Suspend}");
				inlineKeyboard.AddButton("Notify 📢", $"{ActionTag.Notify}");
				inlineKeyboard.AddNewRow();
				inlineKeyboard.AddButton("Command 💻", $"{ActionTag.Command}");
				inlineKeyboard.AddNewRow();
				inlineKeyboard.AddButton("Sleep 🛌", $"{ActionTag.Sleep}");
				break;
		}

		inlineKeyboard.AddButton("🔄", ActionTag.Refresh);
		inlineKeyboard.AddButton("↔️", ActionTag.Tab);
		return inlineKeyboard;
	}

	private static InlineKeyboardMarkup CreateOnOffToggleButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On 🟢", ActionTag.On)
			.AddButton("Off 🔴", ActionTag.Off)
			.AddNewRow()
			.AddButton("Toggle 🔄", ActionTag.Toggle)
			.AddNewRow()
			.AddButton(TimerActive ? "Timer ✅" : "Timer ❌", ActionTag.Timer)
			.AddNewRow()
			.AddButton("<<", ActionTag.Cancel);
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);
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

	private static Dictionary<string, string> DeviceToEmoji = new()
	{
		{ EDevice.Lamp.ToString(), "💡" },
		{ EDevice.Computer.ToString(), "💻" },
		{ EDevice.Power.ToString(), "⚡️" },
		{ EDevice.Generic.ToString(), "🔌" }
	};

	private struct ActionTag
	{
		public const string Type = "device-type";

		public const string On = "device-on";
		public const string Off = "device-off";
		public const string Toggle = "device-toggle";
		public const string Timer = "device-timer";
		public const string Reboot = "device-reboot";
		public const string System = "device-system";
		public const string Suspend = "device-suspend";
		public const string Notify = "device-notify";
		public const string Command = "device-command";
		public const string Sleep = "device-sleep";
		public const string Refresh = "device-refresh";
		public const string Tab = "device-tab";
		public const string Cancel = "device-cancel";
	}
}