using System.Numerics;
using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal sealed class RaspberryModule : IModuleInterface
{
	private static Timer? UpdateTimer = null;
	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Raspberry, out _);

		string messageText = await GetRaspberryInfo();

		if (query != null && query.Message != null)
		{
			try
			{
				await cortana.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, messageText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			}
			catch
			{
				await cortana.AnswerCallbackQuery(query.Id);
			}
			ResetUpdateTimer(cortana, query);
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Raspberry, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh => CreateMenu(cortana, query),
			_ => null

		};

		if (task != null)
		{
			await task;
			return;
		}

		string? response = command switch
		{
			ActionTag.Shutdown => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand("shutdown")),
			ActionTag.Reboot => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand("reboot")),
			_ => null
		};

		if (response != null)
		{
			await cortana.AnswerCallbackQuery(query.Id, response, true);
		}
		else
		{
			switch (command)
			{
				case ActionTag.Command:
					if (Utils.AddChatArg(Utils.Topics.Raspberry, new ChatArgs<List<int>>(EArgsType.RaspberryCommand, query, query.Message, []), query))
					{
						ResetUpdateTimer(cortana, query);
						await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
					}
					break;
				case ActionTag.Cancel:
					if (Utils.ChatArgs.TryGetValue(Utils.Topics.Raspberry, out ChatArgs? value) && value is ChatArgs<List<int>> chatArg)
					{
						if (chatArg.Arg.Count > 0)
						{
							await cortana.DeleteMessages(chatId, chatArg.Arg);
						}
					}
					await CreateMenu(cortana, query);
					break;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		ResetUpdateTimer(cortana, chatArg.Query);
		switch (chatArg.Type)
		{
			case EArgsType.RaspberryCommand:

				if (chatArg is ChatArgs<List<int>> arg)
				{
					string prompt = string.Concat(messageStats.Message[..1].ToLower(), messageStats.Message.AsSpan(1));
					string commandResult = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand($"{EComputerCommand.Command}", prompt));
					Message msg = await Utils.SendToTopic(commandResult, Utils.Topics.Raspberry);
					arg.Arg.Add(messageStats.MessageId);
					arg.Arg.Add(msg.MessageId);
					return;
				}
				break;
		}
	}

	private static async Task<string> GetRaspberryInfo()
	{
		string ip = (await ApiHandler.Get<SensorResponse>($"{ERoute.Raspberry}/{ERaspberryInfo.Ip}")).Match(ip => ip.Value, () => "Unknown");
		string temperature = (await ApiHandler.Get<SensorResponse>($"{ERoute.Raspberry}/{ERaspberryInfo.Temperature}")).Match(temp => $"{Math.Round(double.Parse(temp.Value), 1)}{temp.Unit}", () => "Unknown");
		string location = (await ApiHandler.Get<SensorResponse>($"{ERoute.Raspberry}/{ERaspberryInfo.Location}")).Match(location => location.Value, () => "Unknown");
		string gateway = (await ApiHandler.Get<SensorResponse>($"{ERoute.Raspberry}/{ERaspberryInfo.Gateway}")).Match(gateway => gateway.Value, () => "Unknown");
		return $"\n🍓 <b>Raspberry Info</b>\n\n• 🌡 <b>Temperature</b>: {temperature}\n• 📍 <b>Location</b>: {location}\n• 🌐 <b>Gateway</b>: {gateway}\n• 📬 <b>IP</b>: {ip}\n";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddNewRow()
			.AddButton("Shutdown ⚡️", ActionTag.Shutdown)
			.AddButton("Reboot 🔁", ActionTag.Reboot)
			.AddNewRow()
			.AddButton("Command 💻", ActionTag.Command);
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", ActionTag.Cancel);
	}

	private struct ActionTag
	{
		public const string Shutdown = "raspberry-shutdown";
		public const string Reboot = "raspberry-reboot";
		public const string Command = "raspberry-command";
		public const string Refresh = "raspberry-refresh";
		public const string Cancel = "raspberry-cancel";
	}

	private static void ResetUpdateTimer(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		UpdateTimer?.Destroy();
		UpdateTimer = new Timer("raspberry-updater", new TelegramTimerPayload<(ITelegramBotClient, CallbackQuery?)>(Utils.HomeId, (cortana, query)), async Task (object? sender) =>
		{
			if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

			try
			{
				if (timer.Payload is not TelegramTimerPayload<(ITelegramBotClient cortana, CallbackQuery? query)> payload) return;
				await CreateMenu(payload.Arg.cortana, payload.Arg.query);
			}
			catch { }
		}, ETimerType.Telegram).Set((20, 0, 0));
	}
}