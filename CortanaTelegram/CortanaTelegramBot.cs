using CortanaLib;
using CortanaTelegram.Modules;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram;

public static class CortanaTelegramBot
{
	public static async Task Main()
	{
		var cortana = new TelegramBotClient(DataHandler.Env("CORTANA_TELEGRAM_TOKEN"));
		cortana.StartReceiving(UpdateHandler, ErrorHandler, new ReceiverOptions { DropPendingUpdates = true });

		Utils.Init(cortana);
		await Utils.SendToTopic("I'm Online", Utils.Topics.Log);
		DataHandler.Log("Telegram Bot Online");

		await SignalHandler.WaitForInterrupt();
		Utils.Shutdown();
		DataHandler.Log("Telegram Bot Offline");
	}

	private static async Task UpdateHandler(ITelegramBotClient cortana, Update update, CancellationToken cancellationToken)
	{
		switch (update.Type)
		{
			case UpdateType.CallbackQuery:
				await HandleCallbackQuery(cortana, update.CallbackQuery!);
				break;
			case UpdateType.Message:
				await HandleMessage(cortana, update.Message!);
				break;
			default:
				return;
		}
	}

	private static async Task HandleMessage(ITelegramBotClient cortana, Message message)
	{
		switch (message.Type)
		{
			case MessageType.Text:
				await HandleTextMessage(cortana, message);
				break;
		}
	}

	private static async Task HandleTextMessage(ITelegramBotClient cortana, Message message)
	{
		if (message.From == null || message.From.IsBot || message.Text == null || message.ForwardOrigin != null) return;

		if (message.Chat.Id != Utils.Data.HomeGroup)
		{
			if (message.From.Id != Utils.AuthorId) await cortana.ForwardMessage(Utils.Data.HomeGroup, message.Chat.Id, message.MessageId);
			return;
		}

		var msgData = new MessageData
		{
			TopicId = message.MessageThreadId ?? 0,
			MessageId = message.MessageId,
			Message = message.Text,
			Command = ""
		};

		if (Utils.ChatArgs.TryGetValue(msgData.TopicId, out ChatArgs? chatArg))
		{
			switch (chatArg.Type)
			{
				case EArgsType.Qrcode:
				case EArgsType.Chat:
				case EArgsType.Timer:
				case EArgsType.AudioDownloader:
				case EArgsType.VideoDownloader:
					await UtilityModule.HandleTextMessage(cortana, msgData, chatArg);
					break;
				case EArgsType.Notification:
				case EArgsType.ComputerCommand:
				case EArgsType.HardwareTimer:
					await DeviceModule.HandleTextMessage(cortana, msgData, chatArg);
					break;
				case EArgsType.SetLightThreshold:
				case EArgsType.SetCO2Threshold:
				case EArgsType.SetTvocThreshold:
				case EArgsType.SetMorningHour:
				case EArgsType.SetMotionOffMax:
				case EArgsType.SetMotionOffMin:
					await SensorModule.HandleTextMessage(cortana, msgData, chatArg);
					break;
				case EArgsType.RaspberryCommand:
					await RaspberryModule.HandleTextMessage(cortana, msgData, chatArg);
					break;
			}

			return;
		}

		if (message.Text.StartsWith('/'))
		{
			msgData.Message = msgData.Message[1..];
			msgData.Command = msgData.Message.Split(" ").First().Split("@").First();

			await (msgData.Command == "menu" ? CreateHomeMenu(cortana) : DeviceModule.ExecCommand(msgData, cortana));
		}
		else
		{
			await DeviceModule.HandleKeyboardCallback(cortana, msgData);
		}
	}

	private static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query)
	{
		string command = query.Data!;
		Message message = query.Message!;

		if (message.Chat.Id != Utils.Data.HomeGroup) return;

		var menuTask = command switch
		{
			ActionTag.Device => DeviceModule.CreateMenu(cortana),
			ActionTag.Raspberry => RaspberryModule.CreateMenu(cortana),
			ActionTag.Cortana => CortanaModule.CreateMenu(cortana),
			ActionTag.Utility => UtilityModule.CreateMenu(cortana),
			ActionTag.Sensor => SensorModule.CreateMenu(cortana),
			_ => null
		};

		if (menuTask == null)
		{
			string baseCommand = command.Split("-").First();
			var callbackTask = baseCommand switch
			{
				ActionTag.Device => DeviceModule.HandleCallbackQuery(cortana, query, command),
				ActionTag.Raspberry => RaspberryModule.HandleCallbackQuery(cortana, query, command),
				ActionTag.Cortana => CortanaModule.HandleCallbackQuery(cortana, query, command),
				ActionTag.Utility => UtilityModule.HandleCallbackQuery(cortana, query, command),
				ActionTag.Sensor => SensorModule.HandleCallbackQuery(cortana, query, command),
				_ => null
			};
			if (callbackTask != null) await callbackTask;
		}
		else
		{
			await menuTask;
			await cortana.AnswerCallbackQuery(query.Id);
		}
	}

	private static async Task CreateHomeMenu(ITelegramBotClient cortana)
	{
		const string menuText = "🏠 <b>Home Menu</b>\n\nSelect an option from the keyboard below.";
		await cortana.SendMessage(Utils.Data.HomeGroup, menuText, replyMarkup: CreateMenuButtons(), parseMode: ParseMode.Html);
	}

	private static InlineKeyboardMarkup CreateMenuButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Devices", ActionTag.Device)
			.AddNewRow()
			.AddButton("Sensors", ActionTag.Sensor)
			.AddNewRow()
			.AddButton("Raspberry", ActionTag.Raspberry)
			.AddNewRow()
			.AddButton("Cortana", ActionTag.Cortana)
			.AddNewRow()
			.AddButton("Utility", ActionTag.Utility)
			.AddNewRow()
			.AddButton(InlineKeyboardButton.WithUrl("Cortana", "https://github.com/GwynnN7/Cortana"));
	}

	private static Task ErrorHandler(ITelegramBotClient cortana, Exception exception, CancellationToken cancellationToken)
	{
		string errorMessage = exception switch
		{
			ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
			_ => exception.ToString()
		};
		DataHandler.Log(errorMessage);
		return Task.CompletedTask;
	}

	private struct ActionTag
	{
		public const string Device = "device";
		public const string Raspberry = "raspberry";
		public const string Cortana = "cortana";
		public const string Utility = "utility";
		public const string Sensor = "sensor";
	}
}