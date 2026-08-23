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
	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Raspberry, out _);

		string messageText = await GetRaspberryInfo();

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
			LiveMenu.Track(Utils.Topics.Raspberry, query.Message.MessageId, GetRaspberryInfo, CreateButtons);
		}
		else
		{
			Message sent = await Utils.SendToTopic(messageText, Utils.Topics.Raspberry, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			LiveMenu.Track(Utils.Topics.Raspberry, sent.MessageId, GetRaspberryInfo, CreateButtons);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		if (command == ActionTag.Refresh)
		{
			await CreateMenu(cortana, query);
			return;
		}

		string? response = command switch
		{
			ActionTag.Shutdown => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Shutdown))),
			ActionTag.Reboot => await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Reboot))),
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
		if (chatArg is not ChatArgs<List<int>> arg) return;

		string result = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(nameof(ERaspberryCommand.Command), messageStats.Message));
		Message msg = await Utils.SendToTopic(result, Utils.Topics.Raspberry);
		arg.Arg.Add(messageStats.MessageId);
		arg.Arg.Add(msg.MessageId);
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
		return new InlineKeyboardMarkup()
			.AddButton("Shutdown ⚡️", ActionTag.Shutdown)
			.AddButton("Reboot 🔁", ActionTag.Reboot)
			.AddNewRow()
			.AddButton("Command 💻", ActionTag.Command)
			.AddNewRow()
			.AddButton("Refresh 🔄", ActionTag.Refresh);
	}

	private static InlineKeyboardMarkup CreateCancelButton() => new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);

	private struct ActionTag
	{
		public const string Shutdown = "raspberry-shutdown";
		public const string Reboot = "raspberry-reboot";
		public const string Command = "raspberry-command";
		public const string Refresh = "raspberry-refresh";
		public const string Cancel = "raspberry-cancel";
	}
}
