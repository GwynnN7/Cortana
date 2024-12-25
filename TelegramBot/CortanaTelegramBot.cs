using Kernel.Software;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Modules;
using TelegramBot.Utility;

namespace TelegramBot;

public static class CortanaTelegramBot
{
	private static CancellationTokenSource? _token;
	public static async Task BootTelegramBot()
	{
		var cortana = new TelegramBotClient(FileHandler.Secrets.TelegramToken);
		cortana.StartReceiving(UpdateHandler, ErrorHandler, new ReceiverOptions { DropPendingUpdates = true });

		TelegramUtils.Init(cortana);
		TelegramUtils.SendToUser(TelegramUtils.NameToId("@gwynn7"), "I'm Online", false);
		
		_token = new CancellationTokenSource();
		try
		{
			await Task.Delay(Timeout.Infinite, _token.Token);
		}
		catch (TaskCanceledException)
		{
			Console.WriteLine("Telegram Bot shut down");
		}
	}

	public static async Task StopTelegramBot()
	{
		if(_token != null) await _token.CancelAsync();
	}

	private static Task UpdateHandler(ITelegramBotClient cortana, Update update, CancellationToken cancellationToken)
	{
		switch (update.Type)
		{
			case UpdateType.CallbackQuery:
				HandleCallbackQuery(cortana, update.CallbackQuery!);
				break;
			case UpdateType.Message:
				HandleMessage(cortana, update.Message!);
				break;
			default:
				return Task.CompletedTask;
		}

		return Task.CompletedTask;
	}

	private static void HandleMessage(ITelegramBotClient cortana, Message message)
	{
		switch (message.Type)
		{
			case MessageType.Text:
				HandleTextMessage(cortana, message);
				break;
		}
	}

	private static async void HandleTextMessage(ITelegramBotClient cortana, Message message)
	{
		if (message.From == null || message.From.IsBot || message.Text == null) return;

		var messageStats = new MessageStats
		{
			Message = message,
			ChatId = message.Chat.Id,
			UserId = message.From?.Id ?? message.Chat.Id,
			MessageId = message.MessageId,
			ChatType = message.Chat.Type,
			FullMessage = message.Text,
			Text = message.Text,
			TextList = [],
			Command = ""
		};

		if (TelegramUtils.ChatArgs.TryGetValue(messageStats.ChatId, out TelegramChatArg? chatArg))
		{
			switch (chatArg.Type)
			{
				case ETelegramChatArg.Qrcode:
				case ETelegramChatArg.Chat:
				case ETelegramChatArg.Timer:
				case ETelegramChatArg.AudioDownloader:
				case ETelegramChatArg.VideoDownloader:
					UtilityModule.HandleTextMessage(cortana, messageStats);
					break;
				case ETelegramChatArg.Notification:
				case ETelegramChatArg.Ping:
				case ETelegramChatArg.HardwareTimer:
				case ETelegramChatArg.ComputerCommand:
					HardwareModule.HandleTextMessage(cortana, messageStats);
					break;
				case ETelegramChatArg.Shopping:
					ShoppingModule.HandleTextMessage(cortana, messageStats);
					break;
			}
			
			return;
		}

		if (message.Text.StartsWith('/'))
		{
			messageStats.FullMessage = messageStats.FullMessage[1..];
			messageStats.Command = messageStats.FullMessage.Split(" ").First().Split("@").First();
			messageStats.TextList = messageStats.FullMessage.Split(" ").Skip(1).ToList();
			messageStats.Text = string.Join(" ", messageStats.TextList);

			if (messageStats.Command != "menu")
			{
				HardwareModule.ExecCommand(messageStats, cortana);
				ShoppingModule.ExecCommand(messageStats, cortana);
			}
			else
			{
				CreateHomeMenu(cortana, messageStats.ChatId);
			}
		}
		else
		{
			bool isCallback = await HardwareModule.HandleKeyboardCallback(cortana, messageStats);
			long creatorId = TelegramUtils.NameToId("@gwynn7");
			if (messageStats.UserId == creatorId || messageStats.ChatType != ChatType.Private) return;
			if(isCallback) TelegramUtils.SendToUser(creatorId, $"{TelegramUtils.IdToName(messageStats.UserId)} used Hardware Keyboard");
			else await cortana.ForwardMessage(creatorId, messageStats.ChatId, messageStats.MessageId);
		}
	}

	private static async void HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery)
	{
		string command = callbackQuery.Data!;
		Message message = callbackQuery.Message!;

		switch (command)
		{
			case "home":
				CreateHomeMenu(cortana, message.Chat.Id, message.MessageId);
				break;
			case "automation":
				if (TelegramUtils.CheckHardwarePermission(callbackQuery.From.Id))
					HardwareModule.CreateAutomationMenu(cortana, message);
				else 
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "Sorry, you can't access automation controls");
				break;
			case "raspberry":
				if (TelegramUtils.CheckHardwarePermission(callbackQuery.From.Id))
					HardwareModule.CreateRaspberryMenu(cortana, message);
				else 
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "Sorry, you can't access raspberry's controls");
				break;
			case "hardware_utility":
				if (TelegramUtils.CheckHardwarePermission(callbackQuery.From.Id))
					HardwareModule.CreateHardwareUtilityMenu(cortana, message);
				else 
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "Sorry, you can't access hardware controls");
				break;
			case "software_utility":
				UtilityModule.CreateSoftwareUtilityMenu(cortana, message);
				break;
			default:
				if (command.StartsWith("hardware-")) HardwareModule.HandleCallbackQuery(cortana, callbackQuery, command["hardware-".Length..]);
				else if (command.StartsWith("shopping-")) ShoppingModule.HandleCallbackQuery(cortana, callbackQuery, command["shopping-".Length..]);
				else if (command.StartsWith("utility-")) UtilityModule.HandleCallbackQuery(cortana, callbackQuery, command["utility-".Length..]);
				break;
		}
	}

	private static async void CreateHomeMenu(ITelegramBotClient cortana, long chatId, int? messageId = null)
	{
		if (messageId.HasValue) await cortana.EditMessageText(chatId, messageId.Value, "Cortana Home", replyMarkup: CreateMenuButtons());
		else await cortana.SendMessage(chatId, "Cortana Home", replyMarkup: CreateMenuButtons());
	}

	private static InlineKeyboardMarkup CreateMenuButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Domotica", "automation")
			.AddNewRow()
			.AddButton("Raspberry", "raspberry")
			.AddNewRow()
			.AddButton("Hardware", "hardware_utility")
			.AddNewRow()
			.AddButton("Utility", "software_utility")
			.AddNewRow()
			.AddButton(InlineKeyboardButton.WithUrl("Cortana", "https://github.com/GwynbleiddN7/Cortana"));
	}

	private static Task ErrorHandler(ITelegramBotClient cortana, Exception exception, CancellationToken cancellationToken)
	{
		string errorMessage = exception switch
		{
			ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
			_ => exception.ToString()
		};
		FileHandler.Log("Telegram", errorMessage);
		return Task.CompletedTask;
	}
}