using CortanaDiscord.Runtime;
using CortanaLib.Contracts;
using CortanaLib.Media;
using CortanaLib.Primitives;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Modules;

[Group("utility", "Everyday tools")]
public sealed class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("commands", "What I can do")]
	public Task Commands()
	{
		Embed embed = DiscordContext.Card("Commands", timestamp: false).ToEmbedBuilder()
			.AddField("/home", "The house: devices, sensors, automation, machines, schedules")
			.AddField("/utility", "QR codes, downloads and small tools")
			.AddField("/remind", "Reminders and alarms")
			.AddField("/random", "Random numbers, dice, coins and choices")
			.AddField("/games", "Video game lookups")
			.AddField("/server", "Server moderation")
			.AddField("/settings", "Server settings")
			.Build();

		return RespondAsync(embed: embed);
	}

	[SlashCommand("qrcode", "Turn text into a QR code")]
	public Task QrCode(
		[Summary("content", "What should it contain?")] string content,
		[Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No,
		[Summary("classic", "Plain black and white?")] Answer classic = Answer.No,
		[Summary("border", "Keep the quiet zone?")] Answer border = Answer.Yes) =>
		RespondWithFileAsync(MediaLibrary.CreateQrCode(content, classic == Answer.Yes, border == Answer.Yes),
			"qrcode.png", ephemeral: ephemeral == Answer.Yes);

	[SlashCommand("download-music", "Download a song from YouTube", runMode: RunMode.Async)]
	public async Task DownloadMusic(
		[Summary("video", "YouTube link or search terms")] string query,
		[Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		bool hidden = ephemeral == Answer.Yes;
		await DeferAsync(hidden);

		Result<AudioTrack> track = await MediaLibrary.ResolveTrack(query);
		if (!track.IsOk)
		{
			await FollowupAsync(track.Error, ephemeral: hidden);
			return;
		}

		await FollowupAsync(embed: TrackCard(track.Value, "Downloading..."), ephemeral: hidden);

		Result<Stream> audio = await MediaLibrary.OpenAudioStream(track.Value.OriginalUrl);
		if (!audio.IsOk)
		{
			await FollowupAsync(audio.Error, ephemeral: hidden);
			return;
		}

		await using Stream stream = audio.Value;
		await Context.Channel.SendFileAsync(stream, $"{Sanitize(track.Value.Title)}.mp3");
	}

	[SlashCommand("avatar", "Show someone's profile picture")]
	public Task Avatar(
		[Summary("user", "Whose?")] SocketUser user,
		[Summary("size", "1 to 7, from 64px to 4096px")] [MaxValue(7)] [MinValue(1)] int size = 4)
	{
		string url = user.GetAvatarUrl(size: Convert.ToUInt16(Math.Pow(2, size + 5)));
		Embed embed = DiscordContext.Card("Profile picture", user).ToEmbedBuilder().WithImageUrl(url).Build();

		return RespondAsync(embed: embed);
	}

	[SlashCommand("time-in-voice", "How long you have been in a voice channel")]
	public Task TimeInVoice([Summary("user", "Whose?")] SocketUser? user = null, [Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		user ??= Context.User;

		string text = DiscordContext.VoiceSince.TryGetValue(user.Id, out DateTimeOffset since)
			? $"Connected for {(DateTimeOffset.UtcNow - since):h\\h\\ mm\\m\\ ss\\s}"
			: "Not connected to a voice channel";

		return RespondAsync(embed: DiscordContext.Card(text, user), ephemeral: ephemeral == Answer.Yes);
	}

	[SlashCommand("count", "Count the words and characters in a message")]
	public Task Count([Summary("content", "The text")] string content, [Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		Embed embed = DiscordContext.Card("Word count").ToEmbedBuilder()
			.AddField("Words", content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)
			.AddField("Characters", content.Replace(" ", "").Length)
			.AddField("Characters with spaces", content.Length)
			.Build();

		return RespondAsync(embed: embed, ephemeral: ephemeral == Answer.Yes);
	}

	[SlashCommand("say", "Say something in a channel")]
	public async Task Say([Summary("text", "What should I say?")] string text, [Summary("channel", "Where?")] SocketTextChannel channel)
	{
		try
		{
			await channel.SendMessageAsync(text);
			await RespondAsync("Done", ephemeral: true);
		}
		catch (Exception)
		{
			await RespondAsync("That did not go through, the message is probably too long", ephemeral: true);
		}
	}

	[SlashCommand("whisper", "Send someone a private message")]
	public async Task Whisper([Summary("text", "What should I say?")] string text, [Summary("user", "To whom?")] SocketUser user)
	{
		try
		{
			await user.SendMessageAsync(text);
			await RespondAsync("Done", ephemeral: true);
		}
		catch (Exception)
		{
			await RespondAsync("That did not go through, they may have DMs closed", ephemeral: true);
		}
	}

	private static Embed TrackCard(AudioTrack track, string description) =>
		DiscordContext.Card(track.Title, description: description)
			.ToEmbedBuilder()
			.WithUrl(track.OriginalUrl)
			.WithThumbnailUrl(track.ThumbnailUrl)
			.Build();

	private static string Sanitize(string name) =>
		string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
}

[Group("remind", "Reminders and alarms")]
public sealed class ReminderModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("in", "Remind me after a delay")]
	public Task In(
		[Summary("text", "What should I remind you about?")] string text,
		[Summary("seconds", "Seconds")] [MaxValue(59)] int seconds = 0,
		[Summary("minutes", "Minutes")] [MaxValue(59)] int minutes = 0,
		[Summary("hours", "Hours")] [MaxValue(100)] int hours = 0)
	{
		DateTimeOffset when = DateTimeOffset.Now.AddSeconds(seconds).AddMinutes(minutes).AddHours(hours);
		return Create(text, when);
	}

	[SlashCommand("at", "Remind me at a time")]
	public Task At(
		[Summary("text", "What should I remind you about?")] string text,
		[Summary("hour", "Hour")] [MaxValue(23)] int hour = 0,
		[Summary("minute", "Minute")] [MaxValue(59)] int minute = 0)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		var when = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
		if (when <= now) when = when.AddDays(1);

		return Create(text, when);
	}

	[SlashCommand("list", "Show every saved reminder")]
	public async Task List()
	{
		await DeferAsync(true);
		await FollowupAsync(embed: DiscordContext.Card(await DiscordContext.Text(DiscordContext.Cortana.SchedulesText())), ephemeral: true);
	}

	private async Task Create(string text, DateTimeOffset when)
	{
		await DeferAsync();

		var request = new CreateScheduleRequest(
			$"Reminder: {text}",
			ScheduleTrigger.Once,
			ScheduleActionType.SendNotification,
			nameof(NotificationChannel.Discord),
			$"{Context.User.Mention} {text}",
			when,
			Owner: "discord");

		string result = await DiscordContext.Text(DiscordContext.Cortana.CreateSchedule(request));

		await FollowupAsync(embed: DiscordContext.Card("Reminder saved",
			description: $"For {when:dddd dd MMMM 'at' HH:mm}\n{result}"));
	}
}
