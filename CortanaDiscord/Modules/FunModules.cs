using System.Globalization;
using CortanaDiscord.Runtime;
using CortanaLib.Runtime;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IGDB;
using Game = IGDB.Models.Game;

namespace CortanaDiscord.Modules;

[Group("random", "Random things")]
public sealed class RandomModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("number", "Pick a random number")]
	public Task Number([Summary("min", "Lowest")] int min = 0, [Summary("max", "Highest")] int max = 100,
		[Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No) =>
		RespondAsync(embed: DiscordContext.Card(Random.Shared.Next(min, max).ToString(CultureInfo.InvariantCulture)),
			ephemeral: ephemeral == Answer.Yes);

	[SlashCommand("dice", "Roll one or more dice")]
	public Task Dice([Summary("dice", "How many")] int dice = 1, [Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		string results = string.Join(" ", Enumerable.Range(0, Math.Clamp(dice, 1, 50)).Select(_ => Random.Shared.Next(1, 7)));
		return RespondAsync(embed: DiscordContext.Card(results), ephemeral: ephemeral == Answer.Yes);
	}

	[SlashCommand("coin", "Flip a coin")]
	public Task Coin([Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No) =>
		RespondAsync(embed: DiscordContext.Card(Random.Shared.Next(2) == 0 ? "Heads" : "Tails"), ephemeral: ephemeral == Answer.Yes);

	[SlashCommand("choice", "Pick one of the options")]
	public Task Choice([Summary("options", "Separated by spaces")] string options, [Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		string[] choices = options.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (choices.Length == 0) return RespondAsync("Give me something to choose from", ephemeral: true);

		return RespondAsync(embed: DiscordContext.Card(choices[Random.Shared.Next(choices.Length)]), ephemeral: ephemeral == Answer.Yes);
	}

	[SlashCommand("user", "Pick one of you")]
	public Task User([Summary("everyone", "Include people not in voice?")] Answer everyone = Answer.No,
		[Summary("cortana", "Include me?")] Answer cortana = Answer.No,
		[Summary("ephemeral", "Only you?")] Answer ephemeral = Answer.No)
	{
		IReadOnlyCollection<SocketGuildUser> candidates = Context.Guild.Users;

		if (everyone == Answer.No)
			foreach (SocketVoiceChannel channel in Context.Guild.VoiceChannels)
				if (channel.ConnectedUsers.Contains(Context.User))
					candidates = channel.ConnectedUsers;

		List<SocketGuildUser> users =
		[
			.. candidates.Where(user => !user.IsBot || (user.Id == DiscordContext.Ids.CortanaId && cortana == Answer.Yes))
		];

		if (users.Count == 0) return RespondAsync("There is nobody to choose from", ephemeral: true);

		return RespondAsync($"I choose {users[Random.Shared.Next(users.Count)].Mention}", ephemeral: ephemeral == Answer.Yes);
	}
}

[Group("games", "Video games")]
public sealed class GamesModule : InteractionModuleBase<SocketInteractionContext>
{
	private static readonly Lazy<IGDBClient?> Igdb = new(() =>
	{
		string? client = CortanaEnvironment.Read("CORTANA_IGDB_CLIENT");
		string? secret = CortanaEnvironment.Read("CORTANA_IGDB_SECRET");

		return string.IsNullOrWhiteSpace(client) || string.IsNullOrWhiteSpace(secret) ? null : new IGDBClient(client, secret);
	});

	[SlashCommand("search", "Look a game up on IGDB", runMode: RunMode.Async)]
	public async Task Search([Summary("game", "Game name")] string game)
	{
		await DeferAsync();

		Embed? card = await Card(game, 0);
		if (card == null)
		{
			await FollowupAsync("I could not find that game");
			return;
		}

		IUserMessage message = await FollowupAsync(embed: card);
		await message.ModifyAsync(properties => properties.Components = Controls(game, 0));
	}

	[ComponentInteraction("game-*-*-*", true)]
	public async Task Page(string direction, string game, int index)
	{
		await DeferAsync();

		int wanted = direction == "forward" ? index + 1 : index - 1;
		Embed? card = await Card(game, wanted);
		if (card == null)
		{
			await FollowupAsync("I could not find that game");
			return;
		}

		string counter = card.Fields.First(field => field.Name == "Result").Value;
		int shown = int.Parse(counter.Split(" of ")[0]) - 1;

		await ModifyOriginalResponseAsync(properties =>
		{
			properties.Embed = card;
			properties.Components = Controls(game, shown);
		});
	}

	private static MessageComponent Controls(string game, int index) =>
		new ComponentBuilder()
			.WithButton("<", $"game-backward-{game}-{index}")
			.WithButton(">", $"game-forward-{game}-{index}")
			.Build();

	private static async Task<Embed?> Card(string game, int index)
	{
		if (Igdb.Value is not { } client) return DiscordContext.Card("IGDB is not configured");

		const string fields =
			"cover.image_id, game_engines.name, genres.name, involved_companies.company.name, name, platforms.name, rating, total_rating_count, release_dates.human, summary, themes.name, url";

		Game[]? games = await client.QueryAsync<Game>(IGDBClient.Endpoints.Games,
			$"fields {fields}; search \"{game}\"; where category != (1,2,5,6,7,12); limit 15;");

		if (games == null || games.Length == 0) return null;

		// Most-rated first, then whichever name is closest in length to what was typed
		List<Game> sorted =
		[
			.. games
				.OrderByDescending(entry => entry.TotalRatingCount ?? 0)
				.ThenBy(entry => Math.Abs(entry.Name.Length - game.Length))
		];

		int wanted = index >= sorted.Count ? 0 : index < 0 ? sorted.Count - 1 : index;
		Game found = sorted[wanted];

		string cover = found.Cover != null ? found.Cover.Value.ImageId : "nocover_qhhlj6";

		return DiscordContext.Card(found.Name, timestamp: false).ToEmbedBuilder()
			.WithDescription($"[Open on IGDB]({found.Url})")
			.WithThumbnailUrl($"https://images.igdb.com/igdb/image/upload/t_cover_big/{cover}.jpg")
			.AddField("Result", $"{wanted + 1} of {sorted.Count}")
			.AddField("Rating", found.Rating != null ? Math.Round(found.Rating.Value, 2).ToString(CultureInfo.InvariantCulture) : "N/A")
			.AddField("Released", found.ReleaseDates?.Values.FirstOrDefault()?.Human ?? "N/A")
			.AddField("Themes", found.Themes != null ? string.Join("\n", found.Themes.Values.Take(3).Select(theme => theme.Name)) : "N/A")
			.AddField("Genres", found.Genres != null ? string.Join("\n", found.Genres.Values.Take(3).Select(genre => genre.Name)) : "N/A")
			.AddField("Engine", found.GameEngines?.Values.FirstOrDefault()?.Name ?? "N/A")
			.AddField("Developers", found.InvolvedCompanies != null
				? string.Join("\n", found.InvolvedCompanies.Values.Take(3).Select(company => company.Company.Value.Name))
				: "N/A")
			.AddField("Platforms", found.Platforms != null ? string.Join("\n", found.Platforms.Values.Take(3).Select(platform => platform.Name)) : "N/A")
			.WithFooter(found.Summary is { Length: > 350 } summary ? string.Concat(summary.AsSpan(0, 350), "...") : found.Summary ?? "No summary available")
			.Build();
	}
}
