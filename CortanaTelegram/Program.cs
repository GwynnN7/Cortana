using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using CortanaTelegram.Menus;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

CortanaEnvironment.Load(required: false);

var bot = new TelegramBotClient(CortanaEnvironment.Require("CORTANA_TELEGRAM_TOKEN"));
TelegramSession.Use(bot);

Dictionary<string, Menu> menus = new()
{
	["device"] = new DeviceMenu(),
	["sensor"] = new SensorMenu(),
	["system"] = new SystemMenu(),
	["cortana"] = new CortanaMenu(),
	["utility"] = new UtilityMenu()
};

Dictionary<int, Menu> byTopic = menus.Values.ToDictionary(menu => menu.Topic);

using var lifetime = new CancellationTokenSource();

bot.StartReceiving(HandleUpdate, HandleError, new ReceiverOptions { DropPendingUpdates = true }, lifetime.Token);

_ = Task.Run(() => LiveMenu.Run(lifetime.Token), lifetime.Token);
_ = Task.Run(() => FollowNotifications(lifetime.Token), lifetime.Token);
_ = Task.Run(() => FollowState(lifetime.Token), lifetime.Token);

await TelegramSession.Post("I'm online", TelegramSession.Topics.Log, silent: true);
Log.Write("Telegram", "Online");

await ProcessSignals.WaitForShutdown();
await lifetime.CancelAsync();
Log.Write("Telegram", "Offline");
return;

Task HandleUpdate(ITelegramBotClient client, Update update, CancellationToken token)
{
	_ = Task.Run(async () =>
	{
		try
		{
			switch (update.Type)
			{
				case UpdateType.CallbackQuery:
					await OnCallback(update.CallbackQuery!);
					break;

				case UpdateType.Message when update.Message!.Type == MessageType.Text:
					await OnText(update.Message);
					break;
			}
		}
		catch (Exception ex)
		{
			Log.Error("Telegram", $"Handling an update failed: {ex.Message}");
		}
	}, token);

	return Task.CompletedTask;
}

async Task OnText(Message message)
{
	if (message.From is null or { IsBot: true } || message.Text == null || message.ForwardOrigin != null) return;

	if (message.Chat.Id != TelegramSession.HomeId)
	{
		await bot.ForwardMessage(TelegramSession.HomeId, message.Chat.Id, message.MessageId);
		return;
	}

	int topicId = message.MessageThreadId ?? 0;
	var incoming = new IncomingText(topicId, message.MessageId, message.Text, "");

	if (TelegramSession.Pending.TryGetValue(topicId, out PendingInput? pending))
	{
		if (byTopic.TryGetValue(topicId, out Menu? owner)) await owner.Handle(incoming, pending);
		return;
	}

	if (!message.Text.StartsWith('/')) return;

	string command = message.Text[1..].Split(' ')[0].Split('@')[0];
	await OnCommand(command, incoming);
}

async Task OnCommand(string command, IncomingText incoming)
{
	switch (command)
	{
		case "menu":
			await bot.SendMessage(TelegramSession.HomeId, "🏠 <b>Home menu</b>\n\nPick an area.",
				replyMarkup: HomeKeyboard(), parseMode: ParseMode.Html);
			return;

		case "devices":
			await menus["device"].Show();
			return;

		case "sensors":
			await menus["sensor"].Show();
			return;

		case "system":
			await menus["system"].Show();
			return;

		case "cortana":
			await menus["cortana"].Show();
			return;

		case "utility":
			await menus["utility"].Show();
			return;
	}
}

async Task OnCallback(CallbackQuery query)
{
	if (query.Message?.Chat.Id != TelegramSession.HomeId) return;

	string command = query.Data ?? "";
	string tag = command.Split('-')[0];

	if (!menus.TryGetValue(tag, out Menu? menu)) return;

	TelegramSession.Ack(query);

	if (command == tag) await menu.Show(query);
	else await menu.Handle(query, command);
}

async Task FollowNotifications(CancellationToken token)
{
	while (!token.IsCancellationRequested)
	{
		try
		{
			await foreach (NotificationEnvelope envelope in TelegramSession.Cortana.NotificationStream(NotificationChannel.Telegram, token))
				await TelegramSession.Post(envelope.Notification.Message, TelegramSession.Topics.Log, silent: true);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Log.Write("Telegram", $"The notification stream dropped: {ex.Message}");
			await Task.Delay(TimeSpan.FromSeconds(5), token);
		}
	}
}

async Task FollowState(CancellationToken token)
{
	while (!token.IsCancellationRequested)
	{
		try
		{
			await foreach (CortanaSnapshot _ in TelegramSession.Cortana.SnapshotStream(token)) LiveMenu.Nudge();
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Log.Write("Telegram", $"The state stream dropped: {ex.Message}");
			await Task.Delay(TimeSpan.FromSeconds(5), token);
		}
	}
}

Task HandleError(ITelegramBotClient client, Exception exception, CancellationToken token)
{
	Log.Error("Telegram", exception switch
	{
		ApiRequestException api => $"API error [{api.ErrorCode}] {api.Message}",
		_ => exception.Message
	});

	return Task.CompletedTask;
}

static InlineKeyboardMarkup HomeKeyboard() =>
	new InlineKeyboardMarkup()
		.AddButton("Devices", "device")
		.AddNewRow()
		.AddButton("Sensors", "sensor")
		.AddNewRow()
		.AddButton("System", "system")
		.AddNewRow()
		.AddButton("Cortana", "cortana")
		.AddNewRow()
		.AddButton("Utility", "utility")
		.AddNewRow()
		.AddButton(InlineKeyboardButton.WithUrl("Source", "https://github.com/GwynnN7/Cortana"));
