using CortanaLib.Contracts;
using CortanaLib.Media;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

using VideoQuality = CortanaLib.Primitives.VideoQuality;

namespace CortanaTelegram.Menus;

/// QR codes, reminders, schedules, YouTube downloads and the relay chat with another user
public sealed class UtilityMenu : Menu
{
	private bool _schedulesOpen;
	private VideoQuality _quality = VideoQuality.Balanced;

	public override string Tag => "utility";

	public override int Topic => TelegramSession.Topics.Home;

	protected override async Task<string> Render()
	{
		if (!_schedulesOpen) return "🧰 <b>Utility</b>\n\nPick a tool from the keyboard below.";

		string list = await TelegramSession.Text(TelegramSession.Cortana.SchedulesText());
		return $"⏰ <b>Schedules</b>\n================\n<code>{list}</code>";
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		if (_schedulesOpen)
			return Buttons()
				.AddButton("Delete 🗑", $"{Tag}-deleteschedule")
				.AddNewRow()
				.AddButton("<<", $"{Tag}-cancel");

		return Buttons()
			.AddButton("QR code 📷", $"{Tag}-qr")
			.AddNewRow()
			.AddButton("Start chat 🗣️", $"{Tag}-chat")
			.AddNewRow()
			.AddButton("Reminder ⏱️", $"{Tag}-reminder")
			.AddNewRow()
			.AddButton("Download music 🎵", $"{Tag}-audio")
			.AddNewRow()
			.AddButton("Download video 🎥", $"{Tag}-videomenu")
			.AddNewRow()
			.AddButton("Schedules ⏰", $"{Tag}-schedules");
	}

	public override async Task Handle(CallbackQuery query, string command)
	{
		switch (command)
		{
			case "utility-cancel":
				_schedulesOpen = false;
				await Show(query);
				return;

			case "utility-schedules":
				_schedulesOpen = true;
				await Show(query);
				return;

			case "utility-videomenu":
				await TelegramSession.Bot.EditMessageReplyMarkup(query.Message!.Chat.Id, query.Message.MessageId,
					Buttons()
						.AddButton("Prioritise video", $"{Tag}-video-{VideoQuality.BestVideo}")
						.AddNewRow()
						.AddButton("Prioritise audio", $"{Tag}-video-{VideoQuality.BestAudio}")
						.AddNewRow()
						.AddButton("Balanced", $"{Tag}-video-{VideoQuality.Balanced}")
						.AddNewRow()
						.AddButton("<<", $"{Tag}-cancel"));
				return;

			case var _ when command.StartsWith("utility-video-"):
				_quality = Enum.Parse<VideoQuality>(command["utility-video-".Length..], true);
				await Prompt(query, "video", "Send the YouTube link or search terms");
				return;

			case "utility-qr":
				await Prompt(query, "qr", "What should the QR code contain?");
				return;

			case "utility-audio":
				await Prompt(query, "audio", "Send the YouTube link or search terms");
				return;

			case "utility-reminder":
				await Prompt(query, "reminder", "When? Use a pattern like 30s 10m 2h 1d");
				return;

			case "utility-chat":
				await Prompt(query, "chatuser", "Which user should I relay to? Send their @tag");
				return;

			case "utility-deleteschedule":
				await Prompt(query, "deleteschedule", "Send the id of the schedule to delete");
				return;

			case "utility-leavechat":
				TelegramSession.End(Topic);
				await Show(query);
				return;
		}
	}

	private async Task Prompt(CallbackQuery query, string kind, string text)
	{
		if (!TelegramSession.Begin(Topic, new PendingInput(kind, query, query.Message!), query)) return;

		await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId, text,
			replyMarkup: kind == "chatuser"
				? Buttons().AddButton("Stop chat ⏹️", $"{Tag}-leavechat")
				: TelegramSession.Cancel(Tag));
	}

	public override async Task Handle(IncomingText message, PendingInput pending)
	{
		switch (pending.Kind)
		{
			case "qr":
				await SendQrCode(message);
				return;

			case "reminder":
				await CreateReminder(message, pending);
				return;

			case "deleteschedule":
				await Finish(message, pending, await TelegramSession.Text(TelegramSession.Cortana.DeleteSchedule(message.Text.Trim())));
				return;

			case "audio":
			case "video":
				await Download(message, pending);
				return;

			case "chatuser":
				await StartRelay(message, pending);
				return;

			case "chat":
				if (long.TryParse(pending.Argument, out long userId))
				{
					await TelegramSession.Whisper(userId, message.Text);
					await TelegramSession.Delete(message.MessageId);
				}

				return;
		}
	}

	private async Task SendQrCode(IncomingText message)
	{
		await TelegramSession.Bot.SendChatAction(TelegramSession.HomeId, ChatAction.UploadPhoto);

		Stream image = MediaLibrary.CreateQrCode(message.Text, classicColors: false, quietZone: true);
		image.Position = 0;

		await TelegramSession.Delete(message.MessageId);
		await TelegramSession.Bot.SendPhoto(TelegramSession.HomeId, new InputFileStream(image, "qrcode.png"), messageThreadId: message.TopicId);

		TelegramSession.End(Topic);
		await Show();
	}

	private async Task CreateReminder(IncomingText message, PendingInput pending)
	{
		TimeSpan? delay = TelegramSession.ParseDuration(message.Text);
		await TelegramSession.Delete(message.MessageId);

		if (delay == null)
		{
			TelegramSession.Toast(Topic, "That time pattern is not valid, try again");
			return;
		}

		var request = new CreateScheduleRequest(
			"Reminder",
			ScheduleTrigger.Once,
			ScheduleActionType.SendNotification,
			nameof(NotificationChannel.Telegram),
			"Time is up",
			DateTimeOffset.Now + delay.Value,
			Owner: "telegram");

		await Finish(message, pending, await TelegramSession.Text(TelegramSession.Cortana.CreateSchedule(request)), deleted: true);
	}

	private async Task Download(IncomingText message, PendingInput pending)
	{
		await TelegramSession.Bot.SendChatAction(TelegramSession.HomeId, ChatAction.UploadVideo);

		Result<AudioTrack> track = await MediaLibrary.ResolveTrack(message.Text);
		if (!track.IsOk)
		{
			await TelegramSession.Delete(message.MessageId);
			TelegramSession.Toast(Topic, track.Error);
			return;
		}

		try
		{
			if (pending.Kind == "video")
			{
				Result<string> file = await MediaLibrary.DownloadVideo(track.Value.OriginalUrl, _quality, 50, CortanaEnvironment.Path_(CortanaFolder.Temp));
				if (!file.IsOk) throw new CortanaException(file.Error);

				await using Stream video = File.OpenRead(file.Value);
				await TelegramSession.Bot.SendVideo(TelegramSession.HomeId, InputFile.FromStream(video, track.Value.Title),
					track.Value.Title, messageThreadId: message.TopicId);
			}
			else
			{
				Result<Stream> audio = await MediaLibrary.OpenAudioStream(track.Value.OriginalUrl);
				if (!audio.IsOk) throw new CortanaException(audio.Error);

				await using Stream stream = audio.Value;
				await TelegramSession.Bot.SendAudio(TelegramSession.HomeId, InputFile.FromStream(stream, track.Value.Title),
					messageThreadId: message.TopicId);
			}
		}
		catch (Exception ex)
		{
			TelegramSession.Toast(Topic, $"I could not send that: {ex.Message}");
		}
		finally
		{
			string temporary = CortanaEnvironment.Path_(CortanaFolder.Temp, "temp_video.mp4");
			if (File.Exists(temporary)) File.Delete(temporary);

			await TelegramSession.Delete(message.MessageId);
		}

		TelegramSession.End(Topic);
		await Show();
	}

	private async Task StartRelay(IncomingText message, PendingInput pending)
	{
		long? userId = TelegramSession.Config.IdOf(message.Text.Trim());
		await TelegramSession.Delete(message.MessageId);

		if (userId == null)
		{
			TelegramSession.Toast(Topic, "I do not know that username");
			return;
		}

		TelegramSession.Pending[Topic] = pending with { Kind = "chat", Argument = userId.Value.ToString() };

		await TelegramSession.Bot.EditMessageText(TelegramSession.HomeId, pending.Message.MessageId,
			$"Relaying to {message.Text.Trim()}", replyMarkup: Buttons().AddButton("Stop chat ⏹️", $"{Tag}-leavechat"));
	}

	private async Task Finish(IncomingText message, PendingInput pending, string result, bool deleted = false)
	{
		if (!deleted) await TelegramSession.Delete(message.MessageId);

		TelegramSession.Toast(Topic, result);
		await Show(pending.Query);
	}
}
