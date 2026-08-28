using System.Collections.Concurrent;
using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal sealed class DeviceModule : IModuleInterface
{
		private static readonly ConcurrentDictionary<int, string> SelectedDevice = new();

	private static int _tabIndex;
	private static bool _timerActive;

	private const int TabCount = 1;

	public static async Task ExecCommand(MessageData messageStats, ITelegramBotClient cortana)
	{
		switch (messageStats.Command)
		{
			case "domotica":
				await Utils.SendToTopic("Keyboard Domotica", Utils.Topics.Devices, replyMarkup: CreateHardwareToggles());
				break;
		}
	}

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Devices, out _);

		string messageText = await GetDevicesStatus();

		if (query?.Message != null)
		{
			try
			{
				await cortana.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, messageText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			}
			catch
			{
				await cortana.AnswerCallbackQuery(query.Id);
			}
			LiveMenu.Track(Utils.Topics.Devices, query.Message.MessageId, GetDevicesStatus, CreateButtons);
		}
		else
		{
			Message sent = await Utils.SendToTopic(messageText, Utils.Topics.Devices, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			LiveMenu.Track(Utils.Topics.Devices, sent.MessageId, GetDevicesStatus, CreateButtons);
		}
	}

	public static async Task HandleKeyboardCallback(ITelegramBotClient cortana, MessageData messageStats)
	{
		_ = messageStats.Message switch
		{
			HardwareEmoji.Lamp => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Lamp}"),
			HardwareEmoji.Pc => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Computer}"),
			HardwareEmoji.Generic => await ApiHandler.Post($"{ERoute.Devices}/{EDevice.Generic}"),
			HardwareEmoji.On => await ApiHandler.Post($"{ERoute.Sensors}/settings/{ESettings.AutomaticMode}", new PostValue((int)EStatus.On)),
			HardwareEmoji.Off => await ApiHandler.Post($"{ERoute.Sensors}/settings/{ESettings.AutomaticMode}", new PostValue((int)EStatus.Off)),
			HardwareEmoji.Night => await ApiHandler.Post($"{ERoute.Devices}/sleep"),
			HardwareEmoji.Reboot => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.Reboot}")),
			HardwareEmoji.System => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand($"{EComputerCommand.System}")),
			_ => null
		};
		await cortana.DeleteMessage(Utils.HomeId, messageStats.MessageId);
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		switch (command)
		{
			case ActionTag.Refresh:
				await CreateMenu(cortana, query);
				return;
				return;
			case ActionTag.Timer:
				_timerActive = !_timerActive;
				await cortana.EditMessageReplyMarkup(chatId, messageId, CreateOnOffToggleButtons());
				return;
		}

		string? response = command switch
		{
			ActionTag.Sleep => await ApiHandler.Post($"{ERoute.Devices}/sleep"),
			ActionTag.RoomOn => await ApiHandler.Post($"{ERoute.Devices}/room", new PostAction(nameof(ESwitchAction.On))),
			ActionTag.RoomOff => await ApiHandler.Post($"{ERoute.Devices}/room", new PostAction(nameof(ESwitchAction.Off))),
			_ => null
		};

		if (response != null)
		{
			await cortana.AnswerCallbackQuery(query.Id, response);
			await CreateMenu(cortana, query);
			return;
		}

		switch (command)
		{
			case ActionTag.Cancel:
				if (Utils.ChatArgs.TryGetValue(Utils.Topics.Devices, out ChatArgs? value) && value is ChatArgs<List<int>> { Arg.Count: > 0 } chatArg)
					await cortana.DeleteMessages(chatId, chatArg.Arg);
				await CreateMenu(cortana, query);
				break;

			case ActionTag.On:
			case ActionTag.Off:
			case ActionTag.Toggle:
				await ApplySwitch(cortana, query, command.Split('-').Last(), messageId, chatId);
				break;

			case var _ when command.StartsWith(ActionTag.Type):
				SelectedDevice[messageId] = command.Split('-').Last();
				_timerActive = false;
				await cortana.EditMessageReplyMarkup(chatId, messageId, CreateOnOffToggleButtons());
				break;
		}
	}

	private static async Task ApplySwitch(ITelegramBotClient cortana, CallbackQuery query, string action, int messageId, long chatId)
	{
		if (_timerActive)
		{
			if (Utils.AddChatArg(Utils.Topics.Devices, new ChatArgs<string>(EArgsType.HardwareTimer, query, query.Message!, action), query))
				await cortana.EditMessageText(chatId, messageId, "Timer pattern: {sec}s {min}m {hours}h {days}d", replyMarkup: CreateCancelButton());
			return;
		}

		if (!SelectedDevice.TryRemove(messageId, out string? device))
		{
			await cortana.AnswerCallbackQuery(query.Id, "Pick a device first", true);
			await CreateMenu(cortana, query);
			return;
		}

		string result = await ApiHandler.Post($"{ERoute.Devices}/{device}", new PostAction(action));
		await cortana.AnswerCallbackQuery(query.Id, result);
		await CreateMenu(cortana, query);
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		switch (chatArg.Type)
		{
			case EArgsType.HardwareTimer:
				(int s, int m, int h, int d) times;
				try
				{
					times = Utils.ParseTime(msgData.Message);
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Time pattern is incorrect, try again!", Utils.Topics.Devices, chatArg.Query, false);
					return;
				}

				await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);

				if (!SelectedDevice.TryRemove(chatArg.Message.MessageId, out string? device))
				{
					await Utils.AnswerMessage(cortana, "Device selection was lost, start again", Utils.Topics.Devices, chatArg.Query, false);
					break;
				}

				string action = (chatArg as ChatArgs<string>)!.Arg;
				DateTimeOffset target = DateTimeOffset.Now.AddSeconds(times.s).AddMinutes(times.m).AddHours(times.h);

				var request = new PostSchedule(
					Name: $"{device} {action}",
					Trigger: nameof(EScheduleTrigger.Once),
					ActionType: nameof(EScheduleAction.Device),
					Target: device,
					Value: action,
					At: target,
					Owner: "telegram");

				string created = await ApiHandler.Post($"{ERoute.Schedules}", request);
				await Utils.AnswerMessage(cortana, created, Utils.Topics.Devices, chatArg.Query, false);
				break;

		}

		await CreateMenu(cortana, chatArg.Query);
	}

	private static async Task<string> GetDevicesStatus()
	{
		IOption<DeviceListResponse> devices = await ApiHandler.Get<DeviceListResponse>($"{ERoute.Devices}");

		return devices.Match(
			list =>
			{
				string rows = string.Join("\n", list.Devices.Select(d =>
					$"{(d.Status == nameof(EStatus.On) ? "🟢" : "🔴")} • <b>{d.Device}</b> {DeviceToEmoji.GetValueOrDefault(d.Device, "")}"));
				return $"\n🏠 <b>Devices Status</b>\n================\n{rows}\n";
			},
			() => "\n🏠 <b>Devices Status</b>\n================\nCortana is offline\n");
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		foreach (string element in Enum.GetNames<EDevice>())
			inlineKeyboard.AddButton($"{element} {DeviceToEmoji[element]}", $"{ActionTag.Type}-{element.ToLower()}").AddNewRow();

		inlineKeyboard
			.AddButton("Room On 🏠", ActionTag.RoomOn)
			.AddButton("Room Off 🌑", ActionTag.RoomOff)
			.AddNewRow()
			.AddButton("Sleep 🛌", ActionTag.Sleep)
			.AddNewRow();

		return inlineKeyboard.AddButton("Refresh 🔄", ActionTag.Refresh);
	}

	private static InlineKeyboardMarkup CreateOnOffToggleButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("On 🟢", ActionTag.On)
			.AddButton("Off 🔴", ActionTag.Off)
			.AddNewRow()
			.AddButton("Toggle 🔄", ActionTag.Toggle)
			.AddNewRow()
			.AddButton(_timerActive ? "Set Timer ✅" : "No Timer ❌", ActionTag.Timer)
			.AddNewRow()
			.AddButton("<<", ActionTag.Cancel);
	}

	private static InlineKeyboardMarkup CreateCancelButton() => new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);

	private static ReplyKeyboardMarkup CreateHardwareToggles()
	{
		return new ReplyKeyboardMarkup(true)
			.AddButtons(HardwareEmoji.Lamp, HardwareEmoji.Generic)
			.AddNewRow()
			.AddButtons(HardwareEmoji.Pc, HardwareEmoji.Reboot, HardwareEmoji.System)
			.AddNewRow()
			.AddButtons(HardwareEmoji.On, HardwareEmoji.Night, HardwareEmoji.Off);
	}

	private static readonly Dictionary<string, string> DeviceToEmoji = new()
	{
		{ nameof(EDevice.Lamp), "💡" },
		{ nameof(EDevice.Computer), "💻" },
		{ nameof(EDevice.Power), "⚡️" },
		{ nameof(EDevice.Generic), "🔌" }
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
		public const string Shutdown = "device-shutdown";
		public const string Notify = "device-notify";
		public const string Command = "device-command";
		public const string Metrics = "device-metrics";
		public const string Sleep = "device-sleep";
		public const string RoomOn = "device-roomon";
		public const string RoomOff = "device-roomoff";
		public const string Refresh = "device-refresh";
		public const string Tab = "device-tab";
		public const string Cancel = "device-cancel";
	}
}
