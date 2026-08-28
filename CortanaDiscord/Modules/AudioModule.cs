using System.Collections.Concurrent;
using CortanaDiscord.Utility;
using CortanaDiscord.Voice;
using CortanaLib;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Modules;

[Group("media", "Voice audio")]
public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("play", "Metti qualcosa da youtube", runMode: RunMode.Async)]
	public async Task Play([Summary("video", "YouTube link or search terms")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		bool hidden = ephemeral == EAnswer.Yes;
		await DeferAsync(hidden);

		AudioTrack? track = await ResolveTrack(text, hidden);
		if (track == null) return;

		if (!VoiceService.Play(track, Context.Guild.Id))
		{
			await FollowupAsync("I'm not in a voice channel, I can't play anything", ephemeral: hidden);
			return;
		}

		await FollowupAsync(embed: TrackEmbed(track), components: PlayerControls(), ephemeral: hidden);
	}

	[SlashCommand("nowplaying", "What I'm playing right now")]
	public async Task NowPlaying([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		bool hidden = ephemeral == EAnswer.Yes;
		AudioTrack? track = VoiceService.NowPlaying(Context.Guild.Id);

		if (track == null)
		{
			await RespondAsync("I'm not playing anything", ephemeral: hidden);
			return;
		}

		IReadOnlyCollection<string> queue = VoiceService.GetQueue(Context.Guild.Id);
		Embed embed = TrackEmbed(track).ToEmbedBuilder()
			.WithAuthor("In riproduzione")
			.WithFooter(queue.Count == 0 ? "Nothing queued" : $"{queue.Count} queued")
			.Build();

		await RespondAsync(embed: embed, components: PlayerControls(), ephemeral: hidden);
	}

	[ComponentInteraction(ControlId.Skip, true)]
	public async Task SkipButton() => await AcknowledgeAsync(await VoiceService.Skip(Context.Guild.Id));

	[ComponentInteraction(ControlId.Stop, true)]
	public async Task StopButton() => await AcknowledgeAsync(await VoiceService.Stop(Context.Guild.Id));

	[ComponentInteraction(ControlId.Clear, true)]
	public async Task ClearButton() => await AcknowledgeAsync(VoiceService.Clear(Context.Guild.Id));

	[ComponentInteraction(ControlId.Queue, true)]
	public async Task QueueButton()
	{
		IReadOnlyCollection<string> queue = VoiceService.GetQueue(Context.Guild.Id);
		AudioTrack? current = VoiceService.NowPlaying(Context.Guild.Id);

		string body = current == null ? "" : $"**Ora:** {current.Title}\n\n";
		body += queue.Count == 0 ? "The queue is empty" : string.Join("\n", queue.Select((title, index) => $"`{index + 1}.` {title}"));

		await RespondAsync(embed: DiscordUtils.CreateEmbed("Queue", description: body), ephemeral: true);
	}

	private Task AcknowledgeAsync(string result) => RespondAsync(result, ephemeral: true);

	private static MessageComponent PlayerControls() =>
		new ComponentBuilder()
			.WithButton("Skip", ControlId.Skip, ButtonStyle.Secondary, new Emoji("\u23ED\uFE0F"))
			.WithButton("Queue", ControlId.Queue, ButtonStyle.Secondary, new Emoji("\U0001F4CB"))
			.WithButton("Clear", ControlId.Clear, ButtonStyle.Secondary, new Emoji("\U0001F5D1\uFE0F"))
			.WithButton("Stop", ControlId.Stop, ButtonStyle.Danger, new Emoji("\u23F9\uFE0F"))
			.Build();

	private struct ControlId
	{
		public const string Skip = "media-skip";
		public const string Stop = "media-stop";
		public const string Clear = "media-clear";
		public const string Queue = "media-queue";
	}

	[SlashCommand("skip", "Skip current track")]
	public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = await VoiceService.Skip(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Yes);
	}

	[SlashCommand("clear", "Clear queue")]
	public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = VoiceService.Clear(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Yes);
	}

	[SlashCommand("stop", "Stop track and clear queue")]
	public async Task Stop([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = await VoiceService.Stop(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Yes);
	}

	[SlashCommand("queue", "Show what is queued")]
	public async Task Queue([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Yes)
	{
		IReadOnlyCollection<string> queue = VoiceService.GetQueue(Context.Guild.Id);
		if (queue.Count == 0)
		{
			await RespondAsync("The queue is empty", ephemeral: ephemeral == EAnswer.Yes);
			return;
		}

		string description = string.Join("\n", queue.Select((title, index) => $"`{index + 1}.` {title}"));
		Embed embed = DiscordUtils.CreateEmbed($"Queued ({queue.Count})", description: description);
		await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Yes);
	}

	[SlashCommand("join", "Join the channel I was called from", runMode: RunMode.Async)]
	public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		bool hidden = ephemeral == EAnswer.Yes;

		SocketVoiceChannel? voiceChannel = Context.Guild.VoiceChannels.FirstOrDefault(channel => channel.ConnectedUsers.Contains(Context.User));
		if (voiceChannel == null)
		{
			await RespondAsync("I can't join if you are not in a channel", ephemeral: hidden);
			return;
		}

		await DeferAsync(hidden);
		string text = await VoiceService.Connect(voiceChannel);
		await FollowupAsync(text, ephemeral: hidden);
	}

	[SlashCommand("leave", "Leave the voice channel", runMode: RunMode.Async)]
	public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		bool hidden = ephemeral == EAnswer.Yes;
		await DeferAsync(hidden);
		string text = await VoiceService.Disconnect(Context.Guild.Id);
		await FollowupAsync(text, ephemeral: hidden);
	}

	[SlashCommand("download-music", "Download a song from YouTube", runMode: RunMode.Async)]
	public async Task DownloadMusic([Summary("video", "YouTube link or search terms")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		bool hidden = ephemeral == EAnswer.Yes;
		await DeferAsync(hidden);

		AudioTrack? track = await ResolveTrack(text, hidden);
		if (track == null) return;

		Embed embed = TrackEmbed(track).ToEmbedBuilder().WithDescription("Musica in download...").Build();
		await FollowupAsync(embed: embed, ephemeral: hidden);

		try
		{
			await using Stream stream = await MediaHandler.GetAudioStream(track.OriginalUrl);
			await Context.Channel.SendFileAsync(stream, $"{SanitizeFileName(track.Title)}.mp3");
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Discord] Download failed: {ex.Message}");
			await FollowupAsync("I couldn't download the audio", ephemeral: hidden);
		}
	}

	[SlashCommand("meme", "Play one of the saved memes", runMode: RunMode.Async)]
	public async Task Meme([Summary("nome", "Meme name")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Yes)
	{
		bool hidden = ephemeral == EAnswer.Yes;
		await DeferAsync(hidden);

		KeyValuePair<string, MemeJsonStructure> meme = DiscordUtils.Memes.FirstOrDefault(entry => entry.Value.Alias.Contains(name.ToLower()));
		if (meme.Key == null)
		{
			await FollowupAsync("I have no meme saved under that name", ephemeral: hidden);
			return;
		}

		AudioTrack? track = await ResolveTrack(meme.Value.Link, hidden);
		if (track == null) return;

		Embed embed = DiscordUtils.CreateEmbed(meme.Key, description: $@"{track.Duration:hh\:mm\:ss}")
			.ToEmbedBuilder()
			.WithUrl(track.OriginalUrl)
			.WithThumbnailUrl(track.ThumbnailUrl)
			.Build();

		if (!VoiceService.Play(track, Context.Guild.Id))
		{
			await FollowupAsync("I'm not in a voice channel, I can't play anything", ephemeral: hidden);
			return;
		}

		await FollowupAsync(embed: embed, components: PlayerControls(), ephemeral: hidden);
	}

	[SlashCommand("meme-list", "List the available memes")]
	public async Task GetMemes([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Yes)
	{
		EmbedBuilder embedBuilder = DiscordUtils.CreateEmbed("Memes").ToEmbedBuilder();
		foreach (EMemeCategory category in Enum.GetValues<EMemeCategory>())
		{
			string categoryString = DiscordUtils.Memes
				.Where(meme => meme.Value.Category == category)
				.Aggregate("", (current, meme) => current + $"[{meme.Key}]({meme.Value.Link})\n");

			if (categoryString.Length == 0) continue;
			embedBuilder.AddField(category.ToString(), categoryString);
		}

		await RespondAsync(embed: embedBuilder.Build(), ephemeral: ephemeral == EAnswer.Yes);
	}

	[SlashCommand("meme-fix", "Remove memes that are no longer available", runMode: RunMode.Async)]
	public async Task FixMemes()
	{
		await DeferAsync(ephemeral: true);

		EmbedBuilder embedBuilder = DiscordUtils.CreateEmbed("Memes fixed!").ToEmbedBuilder();

		using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		ConcurrentDictionary<string, MemeJsonStructure> memes = new();
		ConcurrentBag<string> unavailable = [];

		await Task.WhenAll(DiscordUtils.Memes.Select(async pair =>
		{
			try
			{
				string content = await client.GetStringAsync(pair.Value.Link);
				if (content.Contains("video is no longer available") || content.Contains("video unavailable"))
				{
					unavailable.Add(pair.Key);
					return;
				}
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Discord] Could not check meme '{pair.Key}': {ex.Message}");
			}

			memes.TryAdd(pair.Key, pair.Value);
		}));

		foreach (string key in unavailable) embedBuilder.AddField(key, "Video unavailable");
		if (unavailable.IsEmpty) embedBuilder.WithDescription("Erano tutti disponibili");

		DiscordUtils.UpdateMemes(memes.ToDictionary());
		await FollowupAsync(embed: embedBuilder.Build(), ephemeral: true);
	}

	private async Task<AudioTrack?> ResolveTrack(string query, bool hidden)
	{
		string reason = "I found nothing playable for that search";
		try
		{
			AudioTrack? track = await MediaHandler.GetAudioTrack(query);
			if (track != null) return track;
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Discord] Track lookup failed for '{query}': {ex.Message}");
			reason = ex.Message.Contains("not available", StringComparison.OrdinalIgnoreCase)
				? "That video is unavailable: private, removed or blocked in your region"
				: $"I can't read that video: {ex.Message}";
		}

		await FollowupAsync(reason, ephemeral: hidden);
		return null;
	}

	private static Embed TrackEmbed(AudioTrack track) =>
		DiscordUtils.CreateEmbed(track.Title, description: $@"{track.Duration:hh\:mm\:ss}")
			.ToEmbedBuilder()
			.WithUrl(track.OriginalUrl)
			.WithThumbnailUrl(track.ThumbnailUrl)
			.Build();

	private static string SanitizeFileName(string name) =>
		string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
