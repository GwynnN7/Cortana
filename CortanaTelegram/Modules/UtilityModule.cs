using CortanaLib;
using CortanaLib.Structures;
using CortanaTelegram.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using File = System.IO.File;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Modules;

internal sealed class UtilityModule : IModuleInterface
{
	public static async Task CreateMenu(ITelegramBotClient cortana, Message? message = null)
	{
		const string menuText = "🧰 <b>Utility Menu</b>\n\nPick a tool from the keyboard below.";

		if (message != null)
		{
			await cortana.EditMessageText(message.Chat.Id, message.MessageId, menuText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
		else
		{
			await Utils.SendToTopic(menuText, Utils.Topics.Home, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
		}
	}

	public static async Task HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery query, string command)
	{
		int messageId = query.Message!.MessageId;
		long chatId = query.Message.Chat.Id;

		var chatArg = command switch
		{
			ActionTag.QrCode => new ChatArgs(EArgsType.Qrcode, query, query.Message),
			ActionTag.JoinChat => new ChatArgs(EArgsType.Chat, query, query.Message),
			ActionTag.Timer => new ChatArgs(EArgsType.Timer, query, query.Message),
			ActionTag.MusicDownloader => new ChatArgs(EArgsType.AudioDownloader, query, query.Message),
			ActionTag.VideoPriority => new ChatArgs<EVideoQuality>(EArgsType.VideoDownloader, query, query.Message, EVideoQuality.BestVideo),
			ActionTag.AudioPriority => new ChatArgs<EVideoQuality>(EArgsType.VideoDownloader, query, query.Message, EVideoQuality.BestAudio),
			ActionTag.BalancedPriority => new ChatArgs<EVideoQuality>(EArgsType.VideoDownloader, query, query.Message, EVideoQuality.Balanced),
			_ => null
		};

		if (chatArg != null)
		{
			if (Utils.AddChatArg(chatId, chatArg, query))
			{
				switch (command)
				{
					case ActionTag.QrCode:
						await cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton());
						break;
					case ActionTag.JoinChat:
						await cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton());
						break;
					case ActionTag.Timer:
						await cortana.AnswerCallbackQuery(query.Id, "Timer pattern: {sec}s {min}m {hours}h {days}d");
						await cortana.EditMessageText(chatId, messageId, "Set the timer countdown", replyMarkup: CreateCancelButton());
						break;
					case ActionTag.MusicDownloader:
						await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the audio", replyMarkup: CreateCancelButton());
						break;
					case ActionTag.VideoPriority:
					case ActionTag.AudioPriority:
					case ActionTag.BalancedPriority:
						await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton());
						break;
				}
			}
		}
		else
		{
			switch (command)
			{
				case ActionTag.VideoDownloader:
					await cortana.EditMessageText(chatId, messageId, "Choose the download priority", replyMarkup: CreateVideoDownloadButtons());
					break;
				case ActionTag.Cancel:
					await CreateMenu(cortana);
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
				case ActionTag.LeaveChat:
					Utils.ChatArgs.TryGetValue(chatId, out ChatArgs? genericChatArg);
					if (genericChatArg is not ChatArgs<long> { Type: EArgsType.Chat } leaveChatArg) return;
					await cortana.AnswerCallbackQuery(query.Id, $"Chat with {Utils.IdToName(leaveChatArg.Arg)} ended");

					await CreateMenu(cortana);
					Utils.ChatArgs.TryRemove(chatId, out _);
					break;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData)
	{
		switch (Utils.ChatArgs[msgData.ChatId].Type)
		{
			case EArgsType.Qrcode:
				await cortana.SendChatAction(msgData.ChatId, ChatAction.UploadPhoto);
				Stream imageStream = MediaHandler.CreateQrCode(msgData.Message, false, true);
				imageStream.Position = 0;

				await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
				await cortana.SendPhoto(msgData.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
				break;

			case EArgsType.Timer:
				(int s, int m, int h, int d) times;
				try
				{
					times = Utils.ParseTime(msgData.Message);
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Time pattern is incorrect, try again!", Utils.Topics.Home, Utils.ChatArgs[msgData.ChatId].Query);
					return;
				}

				await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);

				var timer = new Timer($":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", new TelegramTimerPayload<string>(msgData.ChatId, null), TelegramTimerFinished, ETimerType.Telegram);
				timer.Set((times.s, times.m, times.h));

				await Utils.AnswerMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", Utils.Topics.Home, Utils.ChatArgs[msgData.ChatId].Query, false);
				break;

			case EArgsType.AudioDownloader:
			case EArgsType.VideoDownloader:
				await cortana.SendChatAction(msgData.ChatId, ChatAction.UploadVideo);
				try
				{
					AudioTrack? track = await MediaHandler.GetAudioTrack(msgData.Message) ?? throw new CortanaException("Video not available");
					switch (Utils.ChatArgs[msgData.ChatId].Type)
					{
						case EArgsType.VideoDownloader:
							{
								await MediaHandler.DownloadVideo(track.OriginalUrl, (Utils.ChatArgs[msgData.ChatId] as ChatArgs<EVideoQuality>)!.Arg, 50, DataHandler.CortanaPath(EDirType.Temp));
								Stream? videoStream = MediaHandler.GetStreamFromFile(DataHandler.CortanaPath(EDirType.Temp, "temp_video.mp4"));
								if (videoStream != null) await cortana.SendVideo(msgData.ChatId, InputFile.FromStream(videoStream, track.Title), track.Title);
								else throw new CortanaException("Video file downloaded not found");
								break;
							}
						case EArgsType.AudioDownloader:
							{
								Stream stream = await MediaHandler.GetAudioStream(track.OriginalUrl);
								await cortana.SendAudio(msgData.ChatId, InputFile.FromStream(stream, track.Title));
								break;
							}
					}
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Sorry, either I can't find that video on YouTube or its size is greater than 50MB", Utils.Topics.Home, Utils.ChatArgs[msgData.ChatId].Query);
				}
				finally
				{
					string path = DataHandler.CortanaPath(EDirType.Temp, "temp_video.mp4");
					if (File.Exists(path)) File.Delete(path);

					await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
				}
				break;

			case EArgsType.Chat:
				await cortana.SendChatAction(msgData.ChatId, ChatAction.Typing);
				if (Utils.ChatArgs[msgData.ChatId] is ChatArgs<long> chatArg)
				{
					await Utils.SendToUser(chatArg.Arg, msgData.Message);
					await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
					return;
				}

				try
				{
					string user = msgData.Message.Trim();
					Utils.ChatArgs[msgData.ChatId] = new ChatArgs<long>(EArgsType.Chat, Utils.ChatArgs[msgData.ChatId].Query, Utils.ChatArgs[msgData.ChatId].Message, Utils.NameToId(user));
					await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
					await cortana.EditMessageText(msgData.ChatId, Utils.ChatArgs[msgData.ChatId].Message.MessageId, $"Currently chatting with {user}", replyMarkup: CreateLeaveButton());
				}
				catch (CortanaException)
				{
					await cortana.DeleteMessage(msgData.ChatId, msgData.MessageId);
					await Utils.AnswerMessage(cortana, "Sorry, I can't find that username. Please try again", Utils.Topics.Home, Utils.ChatArgs[msgData.ChatId].Query);
				}
				return;
			default:
				return;
		}

		await CreateMenu(cortana, Utils.ChatArgs[msgData.ChatId].Message);
		Utils.ChatArgs.TryRemove(msgData.ChatId, out _);
	}

	private static async Task TelegramTimerFinished(object? sender)
	{
		if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

		try
		{
			if (timer.Payload is not TelegramTimerPayload<string> payload) return;
			await Utils.SendToTopic("Timer elapsed!", Utils.Topics.Home);
		}
		catch (Exception e)
		{
			await Utils.SendToTopic($"There was an error with a timer:\n```{e.Message}```", Utils.Topics.Home);
		}
	}

	public static InlineKeyboardMarkup CreateButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("QRCode 📷", ActionTag.QrCode)
			.AddNewRow()
			.AddButton("Start Chat 🗣️", ActionTag.JoinChat)
			.AddNewRow()
			.AddButton("Set Timer ⏱️", ActionTag.Timer)
			.AddNewRow()
			.AddButton("Download Music 🎵", ActionTag.MusicDownloader)
			.AddNewRow()
			.AddButton("Download Video 🎥", ActionTag.VideoDownloader)
	}

	private static InlineKeyboardMarkup CreateVideoDownloadButtons()
	{
		return new InlineKeyboardMarkup()
			.AddButton("Prioritize Video Quality", ActionTag.VideoPriority)
			.AddNewRow()
			.AddButton("Prioritize Audio Quality", ActionTag.AudioPriority)
			.AddNewRow()
			.AddButton("Balance Video and Audio", ActionTag.BalancedPriority)
			.AddNewRow()
			.AddButton("<<", "utility-video");
	}

	private static InlineKeyboardMarkup CreateLeaveButton()
	{
		return new InlineKeyboardMarkup().AddButton("Stop Chat ⏹️", ActionTag.LeaveChat);
	}

	private static InlineKeyboardMarkup CreateCancelButton()
	{
		return new InlineKeyboardMarkup().AddButton("<<", ActionTag.Cancel);
	}

	private struct ActionTag
	{
		public const string QrCode = "utility-qrcode";
		public const string JoinChat = "utility-join";
		public const string LeaveChat = "utility-leave";
		public const string Timer = "utility-timer";
		public const string MusicDownloader = "utility-music";
		public const string VideoDownloader = "utility-video";
		public const string VideoPriority = "utility-video-video_prio";
		public const string AudioPriority = "utility-video-audio_prio";
		public const string BalancedPriority = "utility-video-balanced";
		public const string Cancel = "utility-cancel";

	}
}