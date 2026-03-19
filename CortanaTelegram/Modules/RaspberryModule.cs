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
	public static async Task CreateMenu(ITelegramBotClient cortana, Message? message = null)
	{
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

		string messageText = await GetRaspberryInfo();

		if (message != null)
		{
			await cortana.EditMessageText(message.Chat.Id, message.MessageId, messageText, replyMarkup: CreateButtons());
		}
		else
		{
			await Utils.SendToTopic(messageText, Utils.Topics.Raspberry, replyMarkup: CreateButtons());
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh => CreateMenu(cortana, query.Message),
			ActionTag.Delete => cortana.DeleteMessage(chatId, messageId),
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
					if (Utils.AddChatArg(chatId, new ChatArgs<List<int>>(EArgsType.RaspberryCommand, query, query.Message, []), query))
					{
						await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
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
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats)
	{
		await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);

		switch (Utils.ChatArgs[messageStats.ChatId].Type)
		{
			case EArgsType.RaspberryCommand:
				if (Utils.ChatArgs[messageStats.ChatId] is ChatArgs<List<int>> chatArg)
				{
					string prompt = string.Concat(messageStats.Message[..1].ToLower(), messageStats.Message.AsSpan(1));
					string commandResult = await ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand($"{EComputerCommand.Command}", prompt));
					Message msg = await Utils.SendToTopic(commandResult, Utils.Topics.Raspberry);
					chatArg.Arg.Add(messageStats.MessageId);
					chatArg.Arg.Add(msg.MessageId);
					return;
				}
				break;
		}
		await CreateMenu(cortana, Utils.ChatArgs[messageStats.ChatId].Message);
		Utils.ChatArgs.TryRemove(messageStats.ChatId, out _);
	}

	private static async Task<string> GetRaspberryInfo()
	{
		string ip = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Ip}");
		string temp = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Temperature}");
		string location = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Location}");
		string gateway = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Gateway}");
		return $"Raspberry Info\n\n📬 Public IP: {ip}\n🌡 Temperature: {temp}\n📍 Location: {location}\n🌐 Gateway: {gateway}";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddNewRow()
			.AddButton("Shutdown 💻", ActionTag.Shutdown)
			.AddButton("Reboot 🔁", ActionTag.Reboot)
			.AddNewRow()
			.AddButton("Command 💻", ActionTag.Command)
			.AddNewRow()
			.AddButton("❌", ActionTag.Delete);
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
		public const string Delete = "raspberry-delete";
		public const string Cancel = "raspberry-cancel";
	}
}