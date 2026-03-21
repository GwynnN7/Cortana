using System.Numerics;
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
		await cortana.SendChatAction(Utils.Data.HomeGroup, ChatAction.Typing);

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
			IModuleInterface.ResetUpdateTimer<RaspberryModule>("raspberry-updater", cortana, query);
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
					if (Utils.AddChatArg(chatId, new ChatArgs<List<int>>(EArgsType.RaspberryCommand, query, query.Message, []), query))
					{
						IModuleInterface.DestroyUpdateTimer();
						await cortana.EditMessageText(chatId, messageId, "Commands session is open", replyMarkup: CreateCancelButton());
					}
					break;
				case ActionTag.Cancel:
					if (Utils.ChatArgs.TryGetValue(chatId, out ChatArgs? value) && value is ChatArgs<List<int>> chatArg)
					{
						await cortana.DeleteMessages(chatId, chatArg.Arg);
					}
					await CreateMenu(cortana, query);
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats)
	{
		switch (Utils.ChatArgs[messageStats.ChatId].Type)
		{
			case EArgsType.RaspberryCommand:
				await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
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
}