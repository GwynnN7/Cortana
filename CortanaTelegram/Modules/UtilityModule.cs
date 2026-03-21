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
	public static async Task CreateMenu(ITelegramBotClient cortana, CallbackQuery? query = null)
	{
		Utils.ChatArgs.TryRemove(Utils.Topics.Home, out _);
		const string menuText = "🧰 <b>Utility Menu</b>\n\nPick a tool from the keyboard below.";

		if (query != null && query.Message != null)
		{
			try
			{
				await cortana.EditMessageText(query.Message.Chat.Id, query.Message.MessageId, menuText, replyMarkup: CreateButtons(), parseMode: ParseMode.Html);
			}
			catch
			{
				await cortana.AnswerCallbackQuery(query.Id);
			}

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
			if (Utils.AddChatArg(Utils.Topics.Home, chatArg, query))
			{
				_ = command switch
				{
					ActionTag.QrCode => cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton()),
					ActionTag.JoinChat => cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton()),
					ActionTag.Timer => cortana.EditMessageText(chatId, messageId, "Timer pattern: {sec}s {min}m {hours}h {days}d", replyMarkup: CreateCancelButton()),
					ActionTag.MusicDownloader => cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the audio", replyMarkup: CreateCancelButton()),
					ActionTag.VideoPriority or ActionTag.AudioPriority or ActionTag.BalancedPriority => cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton()),
					_ => Task.CompletedTask
				};
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

					await CreateMenu(cortana, query);
					break;
				case ActionTag.LeaveChat:
					Utils.ChatArgs.TryGetValue(Utils.Topics.Home, out ChatArgs? genericChatArg);
					if (genericChatArg is not ChatArgs<long> { Type: EArgsType.Chat } leaveChatArg) return;
					await cortana.AnswerCallbackQuery(query.Id, $"Chat with {Utils.IdToName(leaveChatArg.Arg)} ended");

					await CreateMenu(cortana, query);
					break;
			}
		}
	}

	public static async Task HandleTextMessage(ITelegramBotClient cortana, MessageData msgData, ChatArgs chatArg)
	{
		await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);

		switch (chatArg.Type)
		{
			case EArgsType.Qrcode:
				await cortana.SendChatAction(Utils.HomeId, ChatAction.UploadPhoto);
				Stream imageStream = MediaHandler.CreateQrCode(msgData.Message, false, true);
				imageStream.Position = 0;

				await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
				await cortana.SendPhoto(Utils.HomeId, new InputFileStream(imageStream, "QRCODE.png"), messageThreadId: msgData.TopicId);
				break;

			case EArgsType.Timer:
				(int s, int m, int h, int d) times;
				try
				{
					times = Utils.ParseTime(msgData.Message);
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Time pattern is incorrect, try again!", Utils.Topics.Home, chatArg.Query);
					return;
				}

				await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);

				var timer = new Timer($":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", new TelegramTimerPayload<string>(Utils.HomeId, null), async Task (object? sender) =>
				{
					if (sender is not Timer { TimerType: ETimerType.Telegram } timer) return;

					try
					{
						if (timer.Payload is not TelegramTimerPayload<string> payload) return;
						await Utils.SendToTopic("Timer elapsed!", msgData.TopicId);
					}
					catch
					{
						await Utils.SendToTopic($"There was an error with a timer", msgData.TopicId);
					}
				}, ETimerType.Telegram).Set((times.s, times.m, times.h));

				await Utils.AnswerMessage(cortana, $"Timer set for {timer.NextTargetTime:HH:mm:ss, dddd dd MMMM}", Utils.Topics.Home, chatArg.Query, false);
				break;

			case EArgsType.AudioDownloader:
			case EArgsType.VideoDownloader:
				await cortana.SendChatAction(Utils.HomeId, ChatAction.UploadVideo);
				try
				{
					AudioTrack? track = await MediaHandler.GetAudioTrack(msgData.Message) ?? throw new CortanaException("Video not available");
					switch (chatArg.Type)
					{
						case EArgsType.VideoDownloader:
							{
								await MediaHandler.DownloadVideo(track.OriginalUrl, (chatArg as ChatArgs<EVideoQuality>)!.Arg, 50, DataHandler.CortanaPath(EDirType.Temp));
								Stream? videoStream = MediaHandler.GetStreamFromFile(DataHandler.CortanaPath(EDirType.Temp, "temp_video.mp4"));
								if (videoStream != null) await cortana.SendVideo(Utils.HomeId, InputFile.FromStream(videoStream, track.Title), track.Title, messageThreadId: msgData.TopicId);
								else throw new CortanaException("Video file downloaded not found");
								break;
							}
						case EArgsType.AudioDownloader:
							{
								Stream stream = await MediaHandler.GetAudioStream(track.OriginalUrl);
								await cortana.SendAudio(Utils.HomeId, InputFile.FromStream(stream, track.Title), messageThreadId: msgData.TopicId);
								break;
							}
					}
				}
				catch
				{
					await Utils.AnswerMessage(cortana, "Sorry, either I can't find that video on YouTube or its size is greater than 50MB", Utils.Topics.Home, chatArg.Query);
				}
				finally
				{
					string path = DataHandler.CortanaPath(EDirType.Temp, "temp_video.mp4");
					if (File.Exists(path)) File.Delete(path);

					await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
				}
				break;

			case EArgsType.Chat:
				await cortana.SendChatAction(Utils.HomeId, ChatAction.Typing);
				if (chatArg is ChatArgs<long> arg)
				{
					await Utils.SendToUser(arg.Arg, msgData.Message);
					await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
					return;
				}

				try
				{
					string user = msgData.Message.Trim();
					Utils.ChatArgs[msgData.TopicId] = new ChatArgs<long>(EArgsType.Chat, chatArg.Query, chatArg.Message, Utils.NameToId(user));
					await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
					await cortana.EditMessageText(Utils.HomeId, chatArg.Message.MessageId, $"Currently chatting with {user}", replyMarkup: CreateLeaveButton());
				}
				catch (CortanaException)
				{
					await cortana.DeleteMessage(Utils.HomeId, msgData.MessageId);
					await Utils.AnswerMessage(cortana, "Sorry, I can't find that username. Please try again", Utils.Topics.Home, chatArg.Query);
				}
				return;
			default:
				return;
		}

		await CreateMenu(cortana, chatArg.Query);
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
			.AddButton("Download Video 🎥", ActionTag.VideoDownloader);
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