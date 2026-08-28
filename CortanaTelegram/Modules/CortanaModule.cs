using System.Collections.Concurrent;
using System.Globalization;
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
	private const int TabCount = 2;
	private static int _tabIndex;

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

			case ActionTag.Tab:
				_tabIndex = (_tabIndex + 1) % TabCount;
				await CreateMenu(cortana, query);
				return;

			case ActionTag.Llm:
				if (Utils.AddChatArg(Utils.Topics.Cortana, new ChatArgs(EArgsType.Llm, query, query.Message), query))
					await cortana.EditMessageText(chatId, messageId, "Ask me anything", replyMarkup: CreateCancelButton());
				return;

			case ActionTag.Brain:
				await cortana.EditMessageText(chatId, messageId, await RenderSettings(), replyMarkup: await CreateSettingsButtons(), parseMode: ParseMode.Html);
				return;

			case var _ when command.StartsWith(ActionTag.Setting):
				string chosen = command[(ActionTag.Setting.Length + 1)..];
				if (Utils.AddChatArg(Utils.Topics.Cortana, new ChatArgs<string>(EArgsType.SetAiSetting, query, query.Message, chosen), query))
					await cortana.EditMessageText(chatId, messageId, $"Send the new value for {chosen}", replyMarkup: CreateCancelButton());
				return;

			case ActionTag.EditPrompt:
				if (Utils.AddChatArg(Utils.Topics.Cortana, new ChatArgs(EArgsType.SetPrompt, query, query.Message), query))
					await cortana.EditMessageText(chatId, messageId, "Send the new system prompt", replyMarkup: CreateCancelButton());
				return;

			case ActionTag.ResetPrompt:
				await cortana.AnswerCallbackQuery(query.Id, await ApiHandler.Delete($"{ERoute.AI}/prompt"));
				await cortana.EditMessageText(chatId, messageId, await RenderSettings(), replyMarkup: await CreateSettingsButtons(), parseMode: ParseMode.Html);
				return;

			case var _ when command.StartsWith(ActionTag.Model):
				await cortana.AnswerCallbackQuery(query.Id, await ApiHandler.Post($"{ERoute.AI}/model", new PostModel(command[(ActionTag.Model.Length + 1)..])));
				await cortana.EditMessageText(chatId, messageId, await RenderSettings(), replyMarkup: await CreateSettingsButtons(), parseMode: ParseMode.Html);
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
		switch (chatArg.Type)
		{
			case EArgsType.Llm:
				string answer = await ApiHandler.Post($"{ERoute.AI}",
					new PostChat(messageStats.Message, $"telegram:{messageStats.TopicId}", "Chief"));
				await Utils.SendToTopic(answer, messageStats.TopicId);
				return;

			case EArgsType.SetPrompt:
				string saved = await ApiHandler.Post($"{ERoute.AI}/prompt", new PostPrompt(messageStats.Message));
				await cortana.DeleteMessage(Utils.HomeId, messageStats.MessageId);
				await Utils.AnswerMessage(cortana, saved, Utils.Topics.Cortana, chatArg.Query, false);
				await CreateMenu(cortana, chatArg.Query);
				return;

			case EArgsType.SetAiSetting:
				string updated = chatArg is ChatArgs<string> setting && double.TryParse(messageStats.Message.Trim(), CultureInfo.InvariantCulture, out double parsed)
					? await ApiHandler.Post($"{ERoute.AI}/settings/{setting.Arg}", new PostNumber(parsed))
					: "That is not a number";
				await cortana.DeleteMessage(Utils.HomeId, messageStats.MessageId);
				await Utils.AnswerMessage(cortana, updated, Utils.Topics.Cortana, chatArg.Query, false);
				await CreateMenu(cortana, chatArg.Query);
				return;
		}

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

		if (_tabIndex == 0)
		{
			inlineKeyboard
				.AddButton("AI 🧠", ActionTag.Llm)
				.AddButton("Settings ⚙️", ActionTag.Brain)
				.AddNewRow();
		}
		else
		{
			foreach (string element in Enum.GetNames<ESubFunctionType>())
				inlineKeyboard.AddButton($"{element.Replace("Cortana", "")} {SubfunctionToEmoji[element]}", $"{ActionTag.Type}-{element.ToLower()}").AddNewRow();

			inlineKeyboard.AddButton("Broadcast 📢", ActionTag.Broadcast).AddNewRow();
		}

		return inlineKeyboard
			.AddButton("Refresh 🔄", ActionTag.Refresh)
			.AddButton("Tab ↔️", ActionTag.Tab);
	}

	private static async Task<string> RenderSettings()
	{
		string models = await ApiHandler.Get($"{ERoute.AI}/models");
		string settings = await ApiHandler.Get($"{ERoute.AI}/settings");
		string prompt = await ApiHandler.Get($"{ERoute.AI}/prompt");

		return $"⚙️ <b>AI Settings</b>\n====================\n<code>{models}</code>\n\n<code>{settings}</code>\n\n<b>Prompt</b>\n<code>{prompt}</code>";
	}

	private static async Task<InlineKeyboardMarkup> CreateSettingsButtons()
	{
		var keyboard = new InlineKeyboardMarkup();

		IOption<ModelListResponse> models = await ApiHandler.Get<ModelListResponse>($"{ERoute.AI}/models");
		models.Match(
			list =>
			{
				foreach (ModelResponse model in list.Models)
					keyboard.AddButton($"{(model.Current ? "✅ " : "")}{model.Name}", $"{ActionTag.Model}-{model.Name}").AddNewRow();
				return true;
			},
			() => false);

		foreach (EAiSetting setting in Enum.GetValues<EAiSetting>())
			keyboard.AddButton($"{setting} ✏️", $"{ActionTag.Setting}-{setting}");

		return keyboard
			.AddNewRow()
			.AddButton("Edit Prompt ✏️", ActionTag.EditPrompt)
			.AddButton("Reset Prompt ♻️", ActionTag.ResetPrompt)
			.AddNewRow()
			.AddButton("<<", ActionTag.Cancel);
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
		public const string Tab = "cortana-tab";
		public const string Llm = "cortana-llm";
		public const string Brain = "cortana-brain";
		public const string Model = "cortana-model";
		public const string Setting = "cortana-setting";
		public const string EditPrompt = "cortana-editprompt";
		public const string ResetPrompt = "cortana-resetprompt";
		public const string Refresh = "cortana-refresh";
		public const string Cancel = "cortana-cancel";
	}
}
