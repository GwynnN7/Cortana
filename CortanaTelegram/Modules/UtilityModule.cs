using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;
using Video = YoutubeExplode.Videos.Video;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal abstract class UtilityModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message message)
	{
		await cortana.EditMessageText(message.Chat.Id, message.Id, "Utility Menu", replyMarkup: CreateButtons());
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
	{
		int messageId = callbackQuery.Message!.MessageId;
		long chatId = callbackQuery.Message.Chat.Id;

		switch (command)
		{
			case "qrcode":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Qrcode, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton());
				break;
			case "join":
				if (callbackQuery.Message.Chat.Type == ChatType.Private)
				{
					if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Chat, callbackQuery, callbackQuery.Message), callbackQuery))
						await cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton());
				}
				else
				{
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "You can only use this command in private chat", true);
				}

				break;
			case "timer":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Timer, callbackQuery, callbackQuery.Message), callbackQuery))
				{
					await cortana.AnswerCallbackQuery(callbackQuery.Id, "Timer pattern: {sec}s {min}m {hours}h {days}d");
					await cortana.EditMessageText(chatId, messageId, "Set the timer countdown", replyMarkup: CreateCancelButton());
				}
				break;
			case "music":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.AudioDownloader, callbackQuery, callbackQuery.Message), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the audio", replyMarkup: CreateCancelButton());
				break;
			case "video":
				await cortana.EditMessageText(chatId, messageId, "Choose Video Quality", replyMarkup: CreateVideoDownloadButtons());
				break;
			case "video-video_prio":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<EVideoQuality>(ETelegramChatArg.VideoDownloader, callbackQuery, callbackQuery.Message, EVideoQuality.BestVideo), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton());
				break;
			case "video-audio_prio":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<EVideoQuality>(ETelegramChatArg.VideoDownloader, callbackQuery, callbackQuery.Message, EVideoQuality.BestAudio), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton());
				break;
			case "video-balanced":
				if (TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg<EVideoQuality>(ETelegramChatArg.VideoDownloader, callbackQuery, callbackQuery.Message, EVideoQuality.Balanced), callbackQuery))
					await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton());
				break;
			case "cancel":
				await CreateMenu(cortana, callbackQuery.Message);
				TelegramUtils.ChatArgs.Remove(chatId);
				return;
			case "leave":
				TelegramUtils.ChatArgs.TryGetValue(chatId, out TelegramChatArg? genericChatArg);
				if (genericChatArg is not TelegramChatArg<long> { Type: ETelegramChatArg.Chat } chatArg) return;
				await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Chat with {TelegramUtils.IdToName(chatArg.Arg)} ended");
				await CreateMenu(cortana, callbackQuery.Message);
				TelegramUtils.ChatArgs.Remove(chatId);
				return;
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
	{
		switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
		{
			case ETelegramChatArg.Qrcode:
				await cortana.SendChatAction(messageStats.ChatId, ChatAction.UploadPhoto);
				Stream imageStream = MediaHandler.CreateQrCode(messageStats.FullMessage, false, true);
				imageStream.Position = 0;
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				await cortana.SendPhoto(messageStats.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
				break;
			case ETelegramChatArg.Timer:
				(int s, int m, int h, int d) times;
				try
				{
					times = TelegramUtils.ParseTime(messageStats.FullMessage);
				}
				catch
				{
					await TelegramUtils.AnswerOrMessage(cortana, "Time pattern is incorrect, try again!", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
					return;
				}
		
				await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
				
				var timer = new Timer($"{messageStats.UserId}:{DateTime.UnixEpoch.Second}", new TelegramTimerPayload<string>(messageStats.ChatId, messageStats.UserId, null), TelegramTimerFinished, ETimerType.Telegram);
				timer.Set((times.s, times.m, times.h));
				
				await TelegramUtils.AnswerOrMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, false);
				await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
				TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
				break;
			case ETelegramChatArg.Chat:
				await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
				if (TelegramUtils.ChatArgs[messageStats.ChatId] is TelegramChatArg<long> chatArg)
				{
					await TelegramUtils.SendToUser(chatArg.Arg , messageStats.FullMessage);
					await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
					break;
				}

				try
				{
					string user = messageStats.FullMessage.Trim();
					TelegramUtils.ChatArgs[messageStats.ChatId] = new TelegramChatArg<long>(ETelegramChatArg.Chat, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery,
						TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage, TelegramUtils.NameToId(user));
					await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
					await cortana.EditMessageText(messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage.MessageId, $"Currently chatting with {user}",
						replyMarkup: CreateLeaveButton());
				}
				catch(CortanaException)
				{
					await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
					await TelegramUtils.AnswerOrMessage(cortana, "Sorry, I can't find that username. Please try again", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
				}

				break;
			case ETelegramChatArg.AudioDownloader:
			case ETelegramChatArg.VideoDownloader:
				await cortana.SendChatAction(messageStats.ChatId, ChatAction.UploadVideo);
				try
				{
					Video videoInfos = await MediaHandler.GetYoutubeVideoInfos(messageStats.FullMessage);
					switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
					{
						case ETelegramChatArg.VideoDownloader:
						{
							await MediaHandler.DownloadVideo(messageStats.FullMessage, (TelegramUtils.ChatArgs[messageStats.ChatId] as TelegramChatArg<EVideoQuality>)!.Arg, 50, DataHandler.CortanaPath(EDirType.Storage));
							Stream? videoStream = MediaHandler.GetStreamFromFile(DataHandler.CortanaPath(EDirType.Storage, "temp_video.mp4"));
							if (videoStream != null) await cortana.SendVideo(messageStats.ChatId, InputFile.FromStream(videoStream, videoInfos.Title), videoInfos.Title);
							else throw new CortanaException("Video file downloaded not found in Storage");
							break;
						}
						case ETelegramChatArg.AudioDownloader:
						{
							Stream stream = await MediaHandler.GetAudioStream(messageStats.FullMessage);
							await cortana.SendAudio(messageStats.ChatId, InputFile.FromStream(stream, videoInfos.Title));
							break;
						}
					}
				}
				catch
				{
					await TelegramUtils.AnswerOrMessage(cortana, "Sorry, either I can't find that video on YouTube or its size is greater than 50MB", messageStats.ChatId,
						TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
				}
				finally
				{
					string path = DataHandler.CortanaPath(EDirType.Storage, "temp_video.mp4");
					if (File.Exists(path)) File.Delete(path);

					await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
					await CreateMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
					TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
				}

				break;
		}
	}
	
	private static async Task TelegramTimerFinished(object? sender)
	{
		if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

		try
		{
			if (timer.Payload is not TelegramTimerPayload<string> payload) return;
			await TelegramUtils.SendToUser(payload.UserId, "Timer elapsed!");
		}
		catch(Exception e)
		{
			await TelegramUtils.SendToUser(TelegramUtils.AuthorId, $"There was an error with a timer:\n```{e.Message}```");
		}
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("QRCode", "utility-qrcode")
			.AddNewRow()
			.AddButton("Start Chat", "utility-join")
			.AddNewRow()
			.AddButton("Set Timer", "utility-timer")
			.AddNewRow()
			.AddButton("Download Music", "utility-music")
			.AddNewRow()
			.AddButton("Download Video", "utility-video")
			.AddNewRow()
			.AddButton("<<", "home");
	}

	private static InlineKeyboardMarkup CreateVideoDownloadButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Prioritize Video Quality", "utility-video-video_prio")
			.AddNewRow()
			.AddButton("Prioritize Audio Quality", "utility-video-audio_prio")
			.AddNewRow()
			.AddButton("Balance Video and Audio", "utility-video-balanced")
			.AddNewRow()
			.AddButton("<<", "software_utility");
	}

	private static InlineKeyboardMarkup CreateLeaveButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Stop Chat", "utility-leave");
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup()
			.AddButton("<<", "utility-cancel");
	}
}