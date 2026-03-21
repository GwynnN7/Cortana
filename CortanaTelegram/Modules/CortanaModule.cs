using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System.Collections.Concurrent;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal sealed class CortanaModule : IModuleInterface
{
	private static readonly ConcurrentDictionary<int, string> SubfunctionAction = new();

	private static Timer? UpdateTimer = null;

	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		string messageText = await GetSubfunctionStatus();

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
			await Utils.SendToTopic(messageText, Utils.Topics.Cortana, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var task = command switch
		{
			ActionTag.Refresh or ActionTag.Cancel => CreateMenu(cortana, query),
			_ => null

		};

		if (task != null)
		{
			await task;
			return;
		}

		switch (command)
		{
			case ActionTag.Start:
			case ActionTag.Stop:
			case ActionTag.Restart:
			case ActionTag.Update:
				string action = command.Split('-').Last();
				SubfunctionAction.TryRemove(messageId, out string? subfuction);
				string result = await ApiHandler.Post($"{ERoute.SubFunctions}/{subfuction}", new PostAction(action));
				await cortana.AnswerCallbackQuery(query.Id, result);
				await CreateMenu(cortana, query);

				break;
			case var _ when command.StartsWith(ActionTag.Type):
				ResetUpdateTimer(cortana, query);
				string subfunctionType = command.Split('-').Last();
				SubfunctionAction[messageId] = subfunctionType;
				await cortana.EditMessageReplyMarkup(chatId, messageId, CreateSubfunctionActionButtons());
				return;
		}

	}


	private static async Task<string> GetSubfunctionStatus()
	{
		string kernel = (await ApiHandler.Get($"{ERoute.SubFunctions}/{ESubFunctionType.CortanaKernel}")).Contains("not") ? "🔴" : "🟢";
		string telegram = (await ApiHandler.Get($"{ERoute.SubFunctions}/{ESubFunctionType.CortanaTelegram}")).Contains("not") ? "🔴" : "🟢";
		string discord = (await ApiHandler.Get($"{ERoute.SubFunctions}/{ESubFunctionType.CortanaDiscord}")).Contains("not") ? "🔴" : "🟢";
		string web = (await ApiHandler.Get($"{ERoute.SubFunctions}/{ESubFunctionType.CortanaWeb}")).Contains("not") ? "🔴" : "🟢";

		return $"🖲 <b>Subfunctions Status</b>\n====================\n{kernel} • <b>Kernel</b> {SubfunctionToEmoji[ESubFunctionType.CortanaKernel.ToString()]}\n{telegram} • <b>Telegram</b> {SubfunctionToEmoji[ESubFunctionType.CortanaTelegram.ToString()]}\n{discord} • <b>Discord</b> {SubfunctionToEmoji[ESubFunctionType.CortanaDiscord.ToString()]}\n{web} • <b>Web</b> {SubfunctionToEmoji[ESubFunctionType.CortanaWeb.ToString()]}";
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		InlineKeyboardMarkup inlineKeyboard = new();

		foreach (string element in Enum.GetNames<ESubFunctionType>())
		{
			inlineKeyboard
				.AddButton($"{SubfunctionToEmoji[element]} - {element} - {SubfunctionToEmoji[element]}", $"{ActionTag.Type}-{element.ToLower()}")
				.AddNewRow();
		}

		inlineKeyboard.AddButton("🔄 - Refresh - 🔄", ActionTag.Refresh);
		return inlineKeyboard;
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

	private static Dictionary<string, string> SubfunctionToEmoji = new()
	{
		{ ESubFunctionType.CortanaKernel.ToString(), "🧠" },
		{ ESubFunctionType.CortanaTelegram.ToString(), "💻" },
		{ ESubFunctionType.CortanaDiscord.ToString(), "💬" },
		{ ESubFunctionType.CortanaWeb.ToString(), "🌐" }
	};

	private struct ActionTag
	{
		public const string Type = "cortana-type";
		public const string Start = "cortana-start";
		public const string Stop = "cortana-stop";
		public const string Restart = "cortana-restart";
		public const string Update = "cortana-update";
		public const string Refresh = "cortana-refresh";
		public const string Cancel = "cortana-cancel";
	}

	private static void ResetUpdateTimer(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		UpdateTimer?.Destroy();
		UpdateTimer = new Timer("cortana-updater", new TelegramTimerPayload<(ITelegramBotClient, CallbackQuery?)>(Utils.HomeId, (cortana, query)), async Task (object? sender) =>
		{
			if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

			try
			{
				if (timer.Payload is not TelegramTimerPayload<(ITelegramBotClient cortana, CallbackQuery? query)> payload) return;
				await CreateMenu(payload.Arg.cortana, payload.Arg.query);
			}
			catch { }
		}, ETimerType.Telegram).Set((30, 0, 0));
	}

	public static Task HandleTextMessage(ITelegramBotClient cortana, MessageData messageStats, ChatArgs chatArg)
	{
		return Task.CompletedTask;
	}
}