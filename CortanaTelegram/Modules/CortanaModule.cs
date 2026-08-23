using System.Collections.Concurrent;
using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Modules;

internal sealed class CortanaModule : IModuleInterface
{
		private static readonly ConcurrentDictionary<int, string> SelectedSubfunction = new();

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
		Utils.ChatArgs.TryRemove(Utils.Topics.Cortana, out _);

		string messageText = await GetSubfunctionStatus();

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
			LiveMenu.Track(Utils.Topics.Cortana, query.Message.MessageId, GetSubfunctionStatus, CreateButtons);
		}
		else
		{
			Message sent = await Utils.SendToTopic(messageText, Utils.Topics.Cortana, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			LiveMenu.Track(Utils.Topics.Cortana, sent.MessageId, GetSubfunctionStatus, CreateButtons);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		switch (command)
		{
			case ActionTag.Refresh:
			case ActionTag.Cancel:
				await CreateMenu(cortana, query);
				return;

			case ActionTag.Broadcast:
				if (Utils.AddChatArg(Utils.Topics.Cortana, new ChatArgs(EArgsType.Broadcast, query, query.Message), query))
					await cortana.EditMessageText(chatId, messageId, "Write the message to broadcast to Discord", replyMarkup: CreateCancelButton());
				return;

			case ActionTag.Start:
			case ActionTag.Stop:
			case ActionTag.Restart:
			case ActionTag.Update:
				string action = command.Split('-').Last();
				if (!SelectedSubfunction.TryRemove(messageId, out string? subfunction))
				{
					await cortana.AnswerCallbackQuery(query.Id, "Pick a subfunction first", true);
					await CreateMenu(cortana, query);
					return;
				}

				string result = await ApiHandler.Post($"{ERoute.SubFunctions}/{subfunction}", new PostAction(action));
				await cortana.AnswerCallbackQuery(query.Id, result);
				await CreateMenu(cortana, query);
				return;

			case var _ when command.StartsWith(ActionTag.Type):
				SelectedSubfunction[messageId] = command.Split('-').Last();
				await cortana.EditMessageReplyMarkup(chatId, messageId, CreateSubfunctionActionButtons());
				return;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats, ChatArgs chatArg)
	{
		string result = await ApiHandler.Post($"{ERoute.SubFunctions}", new PostCommand(nameof(EMessageCategory.Discord), messageStats.Message));
		await cortana.DeleteMessage(Utils.HomeId, messageStats.MessageId);
		await Utils.AnswerMessage(cortana, result, Utils.Topics.Cortana, chatArg.Query, false);
		await CreateMenu(cortana, chatArg.Query);
	}

	private static async Task<string> GetSubfunctionStatus()
	{
		IOption<SubfunctionListResponse> statuses = await ApiHandler.Get<SubfunctionListResponse>($"{ERoute.SubFunctions}");

		return statuses.Match(
			list =>
			{
				string rows = string.Join("\n", list.Subfunctions.Select(s =>
					$"{(s.Running ? "🟢" : "🔴")} • <b>{s.Subfunction.Replace("Cortana", "")}</b> {SubfunctionToEmoji.GetValueOrDefault(s.Subfunction, "")}"));
				return $"🖲 <b>Subfunctions Status</b>\n====================\n{rows}";
			},
			() => "🖲 <b>Subfunctions Status</b>\n====================\nCortana is offline");
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		foreach (string element in Enum.GetNames<ESubFunctionType>())
			inlineKeyboard.AddButton($"{element.Replace("Cortana", "")} {SubfunctionToEmoji[element]}", $"{ActionTag.Type}-{element.ToLower()}").AddNewRow();

		return inlineKeyboard
			.AddButton("Broadcast 📢", ActionTag.Broadcast)
			.AddButton("Refresh 🔄", ActionTag.Refresh);
	}

	private static InlineKeyboardMarkup CreateSubfunctionActionButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Start 🟢", ActionTag.Start)
			.AddButton("Stop 🔴", ActionTag.Stop)
			.AddNewRow()
			.AddButton("Restart 🔄", ActionTag.Restart)
			.AddButton("Update ⏫", ActionTag.Update)
			.AddNewRow()
			.AddButton("<<", ActionTag.Cancel);
	}

	private static InlineKeyboardMarkup CreateCancelButton() => new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);

	private static readonly Dictionary<string, string> SubfunctionToEmoji = new()
	{
		{ nameof(ESubFunctionType.CortanaKernel), "🧠" },
		{ nameof(ESubFunctionType.CortanaTelegram), "✈️" },
		{ nameof(ESubFunctionType.CortanaDiscord), "💬" },
		{ nameof(ESubFunctionType.CortanaWeb), "🌐" }
	};

	private struct ActionTag
	{
		public const string Type = "cortana-type";
		public const string Start = "cortana-start";
		public const string Stop = "cortana-stop";
		public const string Restart = "cortana-restart";
		public const string Update = "cortana-update";
		public const string Broadcast = "cortana-broadcast";
		public const string Refresh = "cortana-refresh";
		public const string Cancel = "cortana-cancel";
	}
}
