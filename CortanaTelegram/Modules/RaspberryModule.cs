using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal sealed class RaspberryModule : IModuleInterface
{
	private const int TabCount = 2;
	private static int _tabIndex;

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Raspberry, out _);

		string messageText = await GetSystemInfo();

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
			LiveMenu.Track(Utils.Topics.Raspberry, query.Message.MessageId, GetSystemInfo, CreateButtons);
		}
		else
		{
			Message sent = await Utils.SendToTopic(messageText, Utils.Topics.Raspberry, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			LiveMenu.Track(Utils.Topics.Raspberry, sent.MessageId, GetSystemInfo, CreateButtons);
		}
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
			case ActionTag.Tab:
				_tabIndex = (_tabIndex + 1) % TabCount;
				await CreateMenu(cortana, query);
				return;
		}

		string? response = command switch
		{
			ActionTag.Shutdown => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Shutdown))),
			ActionTag.Reboot => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Reboot))),
			ActionTag.PcShutdown => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Shutdown))),
			ActionTag.PcReboot => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Reboot))),
			ActionTag.PcSuspend => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Suspend))),
			ActionTag.PcSystem => await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.System))),
			_ => null
		};

		if (response != null)
		{
			await cortana.AnswerCallbackQuery(query.Id, response, true);
			return;
		}

		switch (command)
		{
			case ActionTag.Command:
				if (Utils.AddChatArg(Utils.Topics.Raspberry, new ChatArgs<List<int>>(EArgsType.RaspberryCommand, query, query.Message, []), query))
					await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
				break;

			case ActionTag.PcCommand:
				if (Utils.AddChatArg(Utils.Topics.Raspberry, new ChatArgs<List<int>>(EArgsType.ComputerCommand, query, query.Message, []), query))
					await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
				break;

			case ActionTag.PcNotify:
				if (Utils.AddChatArg(Utils.Topics.Raspberry, new ChatArgs(EArgsType.Notification, query, query.Message), query))
					await cortana.EditMessageText(chatId, messageId, "Write the notification", replyMarkup: CreateCancelButton());
				break;

			case ActionTag.PcLaunch:
				if (Utils.AddChatArg(Utils.Topics.Raspberry, new ChatArgs(EArgsType.Launch, query, query.Message), query))
					await cortana.EditMessageText(chatId, messageId, "Write the application to launch", replyMarkup: CreateCancelButton());
				break;

			case ActionTag.Cancel:
				if (Utils.ChatArgs.TryGetValue(Utils.Topics.Raspberry, out ChatArgs? value) && value is ChatArgs<List<int>> { Arg.Count: > 0 } chatArg)
					await cortana.DeleteMessages(chatId, chatArg.Arg);
				await CreateMenu(cortana, query);
				break;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		switch (chatArg.Type)
		{
			case EArgsType.Notification:
			{
				string sent = await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Notify), messageStats.Message));
				await Utils.AnswerMessage(cortana, sent, Utils.Topics.Raspberry, chatArg.Query, false);
				await CreateMenu(cortana, chatArg.Query);
				return;
			}

			case EArgsType.Launch:
			{
				string launched = await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Launch), messageStats.Message));
				await Utils.AnswerMessage(cortana, launched, Utils.Topics.Raspberry, chatArg.Query, false);
				await CreateMenu(cortana, chatArg.Query);
				return;
			}
		}

		if (chatArg is not ChatArgs<List<int>> arg) return;

		string result = chatArg.Type == EArgsType.ComputerCommand
			? await ApiHandler.Post($"{ERoute.Computer}", new PostCommand(nameof(EComputerCommand.Command), messageStats.Message))
			: await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Command), messageStats.Message));

		Message msg = await Utils.SendToTopic(result, Utils.Topics.Raspberry);
		arg.Arg.Add(messageStats.MessageId);
		arg.Arg.Add(msg.MessageId);
	}

	private static Task<string> GetSystemInfo() => _tabIndex == 0 ? GetComputerInfo() : GetRaspberryInfo();

	private static async Task<string> GetComputerInfo()
	{
		IOption<MetricsResponse> metrics = await ApiHandler.Get<MetricsResponse>($"{ERoute.Computer}/metrics");

		return metrics.Match(
			pc => "\n🖥 <b>" + pc.Host + "</b>\n================\n" +
				$"⚙️ • <b>CPU</b>: {pc.CpuLoad:F0}% - {pc.CpuTemp:F0}°C\n" +
				$"🎮 • <b>GPU</b>: {pc.GpuLoad:F0}% - {pc.GpuTemp:F0}°C\n" +
				$"🧠 • <b>RAM</b>: {pc.MemoryUsed:F1}/{pc.MemoryTotal:F1} GB\n" +
				$"💾 • <b>Disk</b>: {pc.DiskUsed:F0}/{pc.DiskTotal:F0} GB\n" +
				$"⏱ • <b>Uptime</b>: {TimeSpan.FromSeconds(pc.Uptime):d\\d\\ hh\\:mm}\n" +
				(pc.Stale ? $"\n<i>Last seen at {pc.Timestamp:HH:mm}</i>\n" : ""),
			() => "\n🖥 <b>Computer</b>\n================\nNo metrics received yet\n");
	}

	private static async Task<string> GetRaspberryInfo()
	{
		IOption<RaspberryListResponse> info = await ApiHandler.Get<RaspberryListResponse>($"{ERoute.Raspberry}");

		return info.Match(
			list =>
			{
				string Value(ERaspberryInfo key)
				{
					SensorResponse? found = list.Info.FirstOrDefault(i => i.Sensor == key.ToString());
					return found == null || string.IsNullOrEmpty(found.Value) ? "Unknown" : $"{found.Value}{found.Unit}";
				}

				return "\n🍓 <b>Raspberry Info</b>\n================\n" +
					$"🌡 • <b>Temperature</b>: {Value(ERaspberryInfo.Temperature)}\n" +
					$"📍 • <b>Location</b>: {Value(ERaspberryInfo.Location)}\n" +
					$"🌐 • <b>Gateway</b>: {Value(ERaspberryInfo.Gateway)}\n" +
					$"📬 • <b>IP</b>: {Value(ERaspberryInfo.Ip)}\n";
			},
			() => "\n🍓 <b>Raspberry Info</b>\n================\nCortana is offline\n");
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		switch (_tabIndex)
		{
			case 0:
				inlineKeyboard
					.AddButton("Reboot 🔄", ActionTag.PcReboot)
					.AddButton("System 🎮", ActionTag.PcSystem)
					.AddNewRow()
					.AddButton("Suspend 🌙", ActionTag.PcSuspend)
					.AddButton("Shutdown ⏻", ActionTag.PcShutdown)
					.AddNewRow()
					.AddButton("Notify 📢", ActionTag.PcNotify)
					.AddButton("Command 💻", ActionTag.PcCommand)
					.AddNewRow()
					.AddButton("Launch 🚀", ActionTag.PcLaunch)
					.AddNewRow();
				break;

			case 1:
				inlineKeyboard
					.AddButton("Shutdown ⚡️", ActionTag.Shutdown)
					.AddButton("Reboot 🔁", ActionTag.Reboot)
					.AddNewRow()
					.AddButton("Command 💻", ActionTag.Command)
					.AddNewRow();
				break;
		}

		return inlineKeyboard
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddButton("Tab ↔️", ActionTag.Tab);
	}

	private static InlineKeyboardMarkup CreateCancelButton() => new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);

	private struct ActionTag
	{
		public const string Shutdown = "raspberry-shutdown";
		public const string Reboot = "raspberry-reboot";
		public const string Command = "raspberry-command";
		public const string PcShutdown = "raspberry-pcshutdown";
		public const string PcReboot = "raspberry-pcreboot";
		public const string PcSuspend = "raspberry-pcsuspend";
		public const string PcSystem = "raspberry-pcsystem";
		public const string PcNotify = "raspberry-pcnotify";
		public const string PcCommand = "raspberry-pccommand";
		public const string PcLaunch = "raspberry-pclaunch";
		public const string Refresh = "raspberry-refresh";
		public const string Tab = "raspberry-tab";
		public const string Cancel = "raspberry-cancel";
	}
}
