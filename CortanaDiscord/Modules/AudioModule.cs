using System.Collections.Concurrent;
using CortanaDiscord.Handlers;
using CortanaDiscord.Utility;
using CortanaLib;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using YoutubeExplode.Videos;

namespace CortanaDiscord.Modules;

[Group("media", "Gestione audio")]
public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("play", "Metti qualcosa da youtube", runMode: RunMode.Async)]
	public async Task Play([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		await DeferAsync(ephemeral == EAnswer.Si);

		AudioTrack? track = await MediaHandler.GetAudioTrack(text);
		if (track == null)
		{
			await Context.Channel.SendMessageAsync("Il video non è più disponibile su youtube");
			return;
		}
		Embed embed = DiscordUtils.CreateEmbed(track.Title, description: $@"{track.Duration:hh\:mm\:ss}");
		embed = embed.ToEmbedBuilder()
			.WithUrl(track.OriginalUrl)
			.WithThumbnailUrl(track.ThumbnailUrl)
			.Build();

		await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

		bool status = AudioHandler.Play(track, Context.Guild.Id);
		if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mettere audio");
	}

	[SlashCommand("skip", "Skip current track")]
	public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = await AudioHandler.Skip(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("clear", "Clear queue")]
	public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = AudioHandler.Clear(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
	}
	
	[SlashCommand("stop", "Stop track and clear queue")]
	public async Task Stop([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = await AudioHandler.Stop(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("join", "Entro nel canale dove sono stata chiamata")]
	public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		var text = "Non posso connettermi se non sei in un canale";
		foreach (SocketVoiceChannel? voiceChannel in Context.Guild.VoiceChannels)
		{
			if (!voiceChannel.ConnectedUsers.Contains(Context.User)) continue;
			text = AudioHandler.Connect(voiceChannel);
			break;
		}

		await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("leave", "Esco dal canale vocale")]
	public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string text = AudioHandler.Disconnect(Context.Guild.Id);
		await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("download-music", "Scarica una canzone da youtube", runMode: RunMode.Async)]
	public async Task DownloadMusic([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		await DeferAsync(ephemeral == EAnswer.Si);
		
		AudioTrack? track = await MediaHandler.GetAudioTrack(text);
		if (track == null)
		{
			await Context.Channel.SendMessageAsync("Il video non è più disponibile su youtube");
			return;
		}
		Embed embed = DiscordUtils.CreateEmbed(track.Title, description: $@"{track.Duration:hh\:mm\:ss}");
		embed = embed.ToEmbedBuilder()
			.WithDescription("Musica in download...")
			.WithUrl(track.OriginalUrl)
			.WithThumbnailUrl(track.ThumbnailUrl)
			.Build();

		await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

		Stream stream = await MediaHandler.GetAudioStream(track.OriginalUrl);
		await Context.Channel.SendFileAsync(stream, track.Title + ".mp3");
	}

	[SlashCommand("meme", "Metto un meme tra quelli disponibili")]
	public async Task Meme([Summary("nome", "Nome del meme")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
	{
		await DeferAsync(ephemeral == EAnswer.Si);

		try
		{
			(string title, MemeJsonStructure memeStruct) = DiscordUtils.Memes.First(meme => meme.Value.Alias.Contains(name.ToLower()));

			AudioTrack? track = await MediaHandler.GetAudioTrack(memeStruct.Link);
			if (track == null)
			{
				await Context.Channel.SendMessageAsync("L'audio non è più disponibile su youtube");
				return;
			}

			Embed embed = DiscordUtils.CreateEmbed(title, description: $@"{track.Duration:hh\:mm\:ss}");
			embed = embed.ToEmbedBuilder()
				.WithUrl(track.OriginalUrl)
				.WithThumbnailUrl(track.ThumbnailUrl)
				.Build();

			await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

			bool status = AudioHandler.Play(track, Context.Guild.Id);
			if (!status)
				await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mettere audio");
		}
		catch
		{
			await FollowupAsync("Non ho nessun meme salvato con quel nome", ephemeral: ephemeral == EAnswer.Si);
		}
	}

	[SlashCommand("meme-list", "Lista dei meme disponibili")]
	public async Task GetMemes([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
	{
		Embed embed = DiscordUtils.CreateEmbed("Memes");
		var tempEmbed = embed.ToEmbedBuilder();
		foreach (EMemeCategory category in Enum.GetValues<EMemeCategory>())
		{
			string categoryString = DiscordUtils.Memes.Where(meme => meme.Value.Category == category).Aggregate("", (current, meme) => current + $"[{meme.Key}]({meme.Value.Link})\n");
			if (categoryString.Length == 0) continue;
			tempEmbed.AddField(category.ToString(), categoryString);
		}

		await RespondAsync(embed: tempEmbed.Build(), ephemeral: ephemeral == EAnswer.Si);
	}
	
	[SlashCommand("meme-fix", "Rimuovi meme non più disponibili")]
	public async Task FixMemes()
	{
		await DeferAsync(ephemeral: true);
		
		Embed embed = DiscordUtils.CreateEmbed("Memes fixed!");
		var embedBuilder = embed.ToEmbedBuilder();
		
		HttpClient client = new();

		ConcurrentDictionary<string, MemeJsonStructure> memes = new();
		IEnumerable<Task> tasks = DiscordUtils.Memes.Select(async pair =>
		{
			HttpResponseMessage response = await client.GetAsync(pair.Value.Link);
			string content = await response.Content.ReadAsStringAsync();
			if (content.Contains("video non è più disponibile") || content.Contains("video unavailable"))
			{
				lock(embedBuilder) embedBuilder.AddField(pair.Key, "Video unavailable");
				return;
			}
			memes.TryAdd(pair.Key, pair.Value);
		});
		await Task.WhenAll(tasks);
		DiscordUtils.UpdateMemes(memes.ToDictionary());
		
		await FollowupAsync(embed: embedBuilder.Build(), ephemeral: true);
	}
}