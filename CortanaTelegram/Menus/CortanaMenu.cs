using System.Globalization;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// Cortana herself: the chat, the model settings, and the services she runs
public sealed class CortanaMenu : Menu
{
	private static readonly Dictionary<ServiceId, string> Emoji = new()
	{
		[ServiceId.Kernel] = "🧠",
		[ServiceId.Telegram] = "✈️",
		[ServiceId.Discord] = "💬",
		[ServiceId.Web] = "🌐"
	};

	private int _tab;
	private bool _settingsOpen;
	private ServiceId? _selected;

	public override string Tag => "cortana";

	public override int Topic => TelegramSession.Topics.Cortana;

	protected override async Task<string> Render()
	{
		if (_settingsOpen)
		{
			string models = await TelegramSession.Text(TelegramSession.Cortana.ModelsText());
			string settings = await TelegramSession.Text(TelegramSession.Cortana.AiSettingsText());
			string prompt = await TelegramSession.Text(TelegramSession.Cortana.Prompt());

			return $"⚙️ <b>AI settings</b>\n====================\n<code>{models}</code>\n\n<code>{settings}</code>\n\n<b>Prompt</b>\n<code>{prompt}</code>";
		}

		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return "🖲 <b>Services</b>\n====================\nCortana is offline";

		string rows = string.Join("\n", snapshot.Value.Services.Select(view =>
			$"{(view.Running ? "🟢" : "🔴")} • <b>{view.Service}</b> {Emoji[view.Service]}"));

		return $"🖲 <b>Services</b>\n====================\n{rows}";
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		InlineKeyboardMarkup keyboard = Buttons();

		if (_settingsOpen)
		{
			foreach (AiSettingKey setting in Enum.GetValues<AiSettingKey>())
				keyboard.AddButton($"{setting} ✏️", $"{Tag}-set-{setting}").AddNewRow();

			return keyboard
				.AddButton("Model 🧠", $"{Tag}-models")
				.AddNewRow()
				.AddButton("Edit prompt ✏️", $"{Tag}-prompt")
				.AddButton("Reset prompt ♻️", $"{Tag}-resetprompt")
				.AddNewRow()
				.AddButton("<<", $"{Tag}-cancel");
		}

		if (_selected is { } service)
		{
			return keyboard
				.AddButton("Start 🟢", $"{Tag}-do-Start")
				.AddButton("Stop 🔴", $"{Tag}-do-Stop")
				.AddNewRow()
				.AddButton("Restart 🔄", $"{Tag}-do-Restart")
				.AddButton("Update ⏫", $"{Tag}-do-Update")
				.AddNewRow()
				.AddButton($"<< {service}", $"{Tag}-cancel");
		}

		if (_tab == 0)
		{
			keyboard
				.AddButton("Ask 🧠", $"{Tag}-ask")
				.AddButton("Settings ⚙️", $"{Tag}-settings")
				.AddNewRow();
		}
		else
		{
			foreach (ServiceId entry in Enum.GetValues<ServiceId>())
				keyboard.AddButton($"{entry} {Emoji[entry]}", $"{Tag}-pick-{entry}").AddNewRow();

			keyboard.AddButton("Broadcast 📢", $"{Tag}-broadcast").AddNewRow();
		}

		return keyboard
			.AddButton("Refresh 🔄", $"{Tag}-refresh")
			.AddButton("Tab ↔️", $"{Tag}-tab");
	}

	public override async Task Handle(CallbackQuery query, string command)
	{
		switch (command)
		{
			case "cortana-refresh":
			case "cortana-cancel":
				_settingsOpen = false;
				_selected = null;
				await Show(query);
				return;

			case "cortana-tab":
				_tab = (_tab + 1) % 2;
				_settingsOpen = false;
				_selected = null;
				await Show(query);
				return;

			case "cortana-settings":
				_settingsOpen = true;
				await Show(query);
				return;

			case "cortana-ask":
				if (TelegramSession.Begin(Topic, new PendingInput("ask", query, query.Message!), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						"Ask me anything", replyMarkup: TelegramSession.Cancel(Tag));

				return;

			case "cortana-broadcast":
				if (TelegramSession.Begin(Topic, new PendingInput("broadcast", query, query.Message!), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						"What should I say on Discord?", replyMarkup: TelegramSession.Cancel(Tag));

				return;

			case "cortana-prompt":
				if (TelegramSession.Begin(Topic, new PendingInput("prompt", query, query.Message!), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						"Send the new system prompt", replyMarkup: TelegramSession.Cancel(Tag));

				return;

			case "cortana-resetprompt":
				TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.ResetPrompt()));
				await Show(query);
				return;

			case "cortana-models":
				await ShowModels(query);
				return;

			case var _ when command.StartsWith("cortana-model-"):
				TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.SetModel(command["cortana-model-".Length..])));
				await Show(query);
				return;

			case var _ when command.StartsWith("cortana-set-"):
				string setting = command["cortana-set-".Length..];
				if (TelegramSession.Begin(Topic, new PendingInput("aisetting", query, query.Message!, setting), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						$"New value for {setting}", replyMarkup: TelegramSession.Cancel(Tag));

				return;

			case var _ when command.StartsWith("cortana-pick-"):
				_selected = Enum.Parse<ServiceId>(command["cortana-pick-".Length..], true);
				await TelegramSession.Bot.EditMessageReplyMarkup(query.Message!.Chat.Id, query.Message.MessageId, Keyboard());
				return;

			case var _ when command.StartsWith("cortana-do-"):
				if (_selected is not { } target)
				{
					TelegramSession.Toast(Topic, "Pick a service first");
					return;
				}

				var action = Enum.Parse<ServiceAction>(command["cortana-do-".Length..], true);
				_selected = null;

				TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.ControlService(target, action)));
				await Show(query);
				return;
		}
	}

	private async Task ShowModels(CallbackQuery query)
	{
		Result<ModelListResponse> models = await TelegramSession.Cortana.Models();
		InlineKeyboardMarkup keyboard = Buttons();

		if (models.IsOk)
			foreach (ModelView model in models.Value.Models)
				keyboard.AddButton($"{(model.Current ? "✅ " : "")}{model.Name}", $"{Tag}-model-{model.Name}").AddNewRow();

		keyboard.AddButton("<<", $"{Tag}-cancel");

		await TelegramSession.Bot.EditMessageReplyMarkup(query.Message!.Chat.Id, query.Message.MessageId, keyboard);
	}

	public override async Task Handle(IncomingText message, PendingInput pending)
	{
		switch (pending.Kind)
		{
			case "ask":
				string reply = await TelegramSession.Text(
					TelegramSession.Cortana.Ask(message.Text, $"telegram:{message.TopicId}", "Chief"));

				await TelegramSession.Post(reply, message.TopicId);
				return;

			case "prompt":
				await Finish(message, pending, await TelegramSession.Text(TelegramSession.Cortana.SetPrompt(message.Text)));
				return;

			case "aisetting":
				string result = double.TryParse(message.Text.Trim(), CultureInfo.InvariantCulture, out double value)
					? await TelegramSession.Text(TelegramSession.Cortana.SetAiSetting(Enum.Parse<AiSettingKey>(pending.Argument), value))
					: "That is not a number";

				await Finish(message, pending, result);
				return;

			case "broadcast":
				await Finish(message, pending, await TelegramSession.Text(TelegramSession.Cortana.Notify(
					new NotifyRequest(message.Text, NotificationSource.Kernel, NotificationLevel.Info, NotificationChannel.Discord))));
				return;
		}
	}

	private async Task Finish(IncomingText message, PendingInput pending, string result)
	{
		await TelegramSession.Delete(message.MessageId);
		TelegramSession.Toast(Topic, result);
		await Show(pending.Query);
	}
}
