using CortanaDiscord.Handlers;
using CortanaDiscord.Utility;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Utility;
using YoutubeExplode.Videos;

namespace CortanaDiscord.Modules;

[Group("media", "Gestione audio")]
public class AudioModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("play", "Metti qualcosa da youtube", runMode: RunMode.Async)]
	public async Task Play([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		await DeferAsync(ephemeral == EAnswer.Si);

		Video result = await MediaHandler.GetYoutubeVideoInfos(text);
		TimeSpan duration = result.Duration ?? TimeSpan.Zero;
		Embed embed = DiscordUtils.CreateEmbed(result.Title, description: $@"{duration:hh\:mm\:ss}");
		embed = embed.ToEmbedBuilder()
			.WithUrl(result.Url)
			.WithThumbnailUrl(result.Thumbnails[^1].Url)
			.Build();

		await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

		bool status = await AudioHandler.PlayMusic(result.Url, Context.Guild.Id);
		if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il video");
	}

	[SlashCommand("skip", "Skippa quello che sto dicendo")]
	public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = AudioHandler.Skip(Context.Guild.Id);
		await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("stop", "Rimuovi tutto quello che c'è in coda")]
	public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		string result = AudioHandler.Clear(Context.Guild.Id);
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

	[SlashCommand("scarica-musica", "Scarica una canzone da youtube", runMode: RunMode.Async)]
	public async Task DownloadMusic([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
	{
		await DeferAsync(ephemeral == EAnswer.Si);

		Video result = await MediaHandler.GetYoutubeVideoInfos(text);
		TimeSpan duration = result.Duration ?? TimeSpan.Zero;
		Embed embed = DiscordUtils.CreateEmbed(result.Title, description: $@"{duration:hh\:mm\:ss}");
		embed = embed.ToEmbedBuilder()
			.WithDescription("Musica in download...")
			.WithUrl(result.Url)
			.WithThumbnailUrl(result.Thumbnails[^1].Url)
			.Build();

		await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

		Stream stream = await MediaHandler.GetAudioStream(result.Url);
		await Context.Channel.SendFileAsync(stream, result.Title + ".mp3");
	}

	[SlashCommand("meme", "Metto un meme tra quelli disponibili")]
	public async Task Meme([Summary("nome", "Nome del meme")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
	{
		await DeferAsync(ephemeral == EAnswer.Si);

		foreach ((string title, MemeJsonStructure memeStruct) in DiscordUtils.Memes)
		{
			if (!memeStruct.Alias.Contains(name.ToLower())) continue;
			string link = memeStruct.Link;

			Video result = await MediaHandler.GetYoutubeVideoInfos(link);
			TimeSpan duration = result.Duration ?? TimeSpan.Zero;
			Embed embed = DiscordUtils.CreateEmbed(title, description: $@"{duration:hh\:mm\:ss}");
			embed = embed.ToEmbedBuilder()
				.WithUrl(result.Url)
				.WithThumbnailUrl(result.Thumbnails[^1].Url)
				.Build();

			await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

			bool status = await AudioHandler.PlayMusic(result.Url, Context.Guild.Id);
			if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mettere il meme");
			return;
		}

		await FollowupAsync("Non ho nessun meme salvato con quel nome", ephemeral: ephemeral == EAnswer.Si);
	}

	[SlashCommand("elenco-meme", "Lista dei meme disponibili")]
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
}