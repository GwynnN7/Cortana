using System.Globalization;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Utility;
using IGDB;
using Kernel.Software;
using Game = IGDB.Models.Game;

namespace DiscordBot.Modules;

internal class UtilityModule : InteractionModuleBase<SocketInteractionContext>
{
	[Group("utility", "Comandi di utilità")]
	public abstract class UtilityGroup : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("comandi", "Vi mostro le categorie dei miei comandi")]
		public async Task ShowCommands()
		{
			Embed commandsEmbed = DiscordUtils.CreateEmbed("Comandi", withTimeStamp: false);
			commandsEmbed = commandsEmbed.ToEmbedBuilder()
				.AddField("/utility", "Funzioni di utility")
				.AddField("/media", "Gestione audio dei canali vocali")
				.AddField("/domotica", "Domotica personale riservata")
				.AddField("/timer", "Gestione timer e sveglie")
				.AddField("/random", "Scelte random")
				.AddField("/games", "Comandi per videogames")
				.AddField("/gestione", "Gestione Server Discord")
				.AddField("/settings", "Impostazioni Server Discord")
				.Build();
			await RespondAsync(embed: commandsEmbed);
		}

		// From Kernel

		[SlashCommand("qrcode", "Creo un QRCode con quello che mi dite")]
		public async Task CreateQr([Summary("contenuto", "Cosa vuoi metterci?")] string content, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No,
			[Summary("colore-base", "Vuoi il colore bianco normale?")]
			EAnswer normalColor = EAnswer.No, [Summary("bordo", "Vuoi aggiungere il bordo?")] EAnswer quietZones = EAnswer.Si)
		{
			Stream imageStream = MediaHandler.CreateQrCode(content, normalColor == EAnswer.Si, quietZones == EAnswer.Si);

			await RespondWithFileAsync(imageStream, "QRCode.png", ephemeral: ephemeral == EAnswer.Si);
		}

		// Discord-specific

		[SlashCommand("avatar", "Vi mando la vostra immagine profile")]
		public async Task GetAvatar([Summary("user", "Di chi vuoi vedere l'immagine?")] SocketUser user,
			[Summary("grandezza", "Grandezza dell'immagine [Da 64px a 4096px, inserisci un numero da 1 a 7]")] [MaxValue(7)] [MinValue(1)]
			int size = 4)
		{
			string url = user.GetAvatarUrl(size: Convert.ToUInt16(Math.Pow(2, size + 5)));
			Embed embed = DiscordUtils.CreateEmbed("Profile Picture", user);
			embed = embed.ToEmbedBuilder().WithImageUrl(url).Build();
			await RespondAsync(embed: embed);
		}

		[SlashCommand("tempo-in-voice", "Da quanto tempo state in chat vocale?")]
		public async Task TimeConnected([Summary("user", "A chi è rivolto?")] SocketUser? user = null, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
		{
			user ??= Context.User;
			if (DiscordUtils.TimeConnected.TryGetValue(user.Id, out DateTime time))
			{
				TimeSpan deltaTime = DateTime.Now.Subtract(time);
				var connectionTime = "Sei connesso da";
				if (deltaTime.Hours > 0) connectionTime += $" {deltaTime.Hours} ore";
				if (deltaTime.Minutes > 0) connectionTime += $" {deltaTime.Minutes} minuti";
				if (deltaTime.Seconds > 0) connectionTime += $" {deltaTime.Seconds} secondi";

				Embed embed = DiscordUtils.CreateEmbed(connectionTime, user);
				await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
			}
			else
			{
				Embed embed = DiscordUtils.CreateEmbed("Non connesso alla chat vocale", user);
				await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
			}
		}

		[SlashCommand("conta-parole", "Scrivi un messaggio e ti dirò quante parole e caratteri ci sono")]
		public async Task CountWorld([Summary("contenuto", "Cosa vuoi metterci?")] string content, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
		{
			Embed embed = DiscordUtils.CreateEmbed("Conta Parole");
			embed = embed.ToEmbedBuilder()
				.AddField("Parole", content.Split(" ").Length)
				.AddField("Caratteri", content.Replace(" ", "").Length)
				.AddField("Caratteri con spazi", content.Length)
				.Build();

			await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
		}

		[SlashCommand("scrivi", "Scrivo qualcosa al posto vostro")]
		public async Task WriteSomething([Summary("testo", "Cosa vuoi che dica?")] string text, [Summary("canale", "In che canale vuoi che scriva?")] SocketTextChannel channel)
		{
			try
			{
				await channel.SendMessageAsync(text);
				await RespondAsync("Fatto", ephemeral: true);
			}
			catch
			{
				await RespondAsync("C'è stato un problema, probabilmente il messaggio è troppo lungo", ephemeral: true);
			}
		}

		[SlashCommand("scrivi-in-privato", "Scrivo in privato qualcosa a chi volete")]
		public async Task WriteSomethingInDm([Summary("testo", "Cosa vuoi che dica?")] string text, [Summary("user", "Vuoi mandarlo in privato a qualcuno?")] SocketUser user)
		{
			try
			{
				await user.SendMessageAsync(text);
				await RespondAsync("Fatto", ephemeral: true);
			}
			catch
			{
				await RespondAsync("C'è stato un problema, probabilmente il messaggio è troppo lungo", ephemeral: true);
			}
		}

		[SlashCommand("code", "Converto un testo sotto forma di codice")]
		public async Task ToCode()
		{
			await RespondWithModalAsync<CodeModal>("to-code");
		}

		[ModalInteraction("to-code", true)]
		public async Task CodeModalResponse(CodeModal modal)
		{
			string text = modal.Text;
			if (text.Length >= 1500)
			{
				await RespondAsync(string.Concat("```", text.AsSpan(0, 1000), "```"));
				await Context.Channel.SendMessageAsync(string.Concat("```", text.AsSpan(1000), "```"));
			}
			else
			{
				await RespondAsync("```" + text + "```");
			}
		}

		public abstract class CodeModal : IModal
		{
			[InputLabel("Cosa vuoi convertire?")]
			[ModalTextInput("text", TextInputStyle.Paragraph, "Scrivi qui...")]
			public required string Text { get; set; }

			public string Title => "Codice";
		}
	}

	[Group("games", "videogames")]
	public class VideogamesGroup : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("igdb", "Cerco uno o più giochi su IGDB")]
		public async Task SearchGame([Summary("game", "Nome del gioco")] string game)
		{
			await DeferAsync();

			Embed? gameEmbed = await GetGameEmbedAsync(game, 0);
			if (gameEmbed == null)
			{
				await FollowupAsync("Mi dispiace, non ho trovato il gioco che stavi cercando");
				return;
			}

			IUserMessage? mex = await FollowupAsync(embed: gameEmbed);
			MessageComponent messageComponent = new ComponentBuilder()
				.WithButton("<", $"game-backward-{game}-0-{mex.Id}")
				.WithButton(">", $"game-forward-{game}-0-{mex.Id}")
				.Build();

			await mex.ModifyAsync(messageProperties => messageProperties.Components = messageComponent);
		}

		private static async Task<Embed?> GetGameEmbedAsync(string game, int count)
		{
			var igdb = new IGDBClient(FileHandler.Secrets.IgdbClient, FileHandler.Secrets.IgdbSecret);
			const string fields =
				"cover.image_id, game_engines.name, genres.name, involved_companies.company.name, name, platforms.name, rating, total_rating_count, release_dates.human, summary, themes.name, url";
			var query = $"fields {fields}; search \"{game}\"; where category != (1,2,5,6,7,12); limit 15;";
			Game[]? games = await igdb.QueryAsync<Game>(IGDBClient.Endpoints.Games, query);

			List<Game> sortedGames = games.ToList();
			sortedGames.Sort(delegate(Game a, Game b)
			{
				if (a.TotalRatingCount is null or 0 && b.TotalRatingCount is null or 0)
				{
					if (Math.Abs(a.Name.Length - game.Length) <= Math.Abs(b.Name.Length - game.Length)) return -1;
					return 1;
				}

				if (a.TotalRatingCount is null or 0) return 1;
				if (b.TotalRatingCount is null or 0) return -1;
				if (a.TotalRatingCount >= b.TotalRatingCount) return -1;
				return 1;
			});

			if (sortedGames.Count == 0) return null;
			if (count >= sortedGames.Count) count = 0;
			else if (count < 0) count = sortedGames.Count - 1;

			Game foundGame = sortedGames[count];

			string coverId = foundGame.Cover != null ? foundGame.Cover.Value.ImageId : "nocover_qhhlj6";
			Embed gameEmbed = DiscordUtils.CreateEmbed(foundGame.Name, withTimeStamp: false);
			gameEmbed = gameEmbed.ToEmbedBuilder()
				.WithDescription($"[Vai alla pagina IGDB]({foundGame.Url})")
				.WithThumbnailUrl($"https://images.igdb.com/igdb/image/upload/t_cover_big/{coverId}.jpg")
				.AddField("Risultato", $"{count + 1} di {sortedGames.Count}")
				.AddField("Rating", foundGame.Rating != null ? Math.Round(foundGame.Rating.Value, 2).ToString(CultureInfo.CurrentCulture) : "N/A")
				.AddField("Release Date", foundGame.ReleaseDates != null ? foundGame.ReleaseDates.Values.First().Human : "N/A")
				.AddField("Themes", foundGame.Themes != null ? string.Join("\n", foundGame.Themes.Values.Take(3).Select(x => x.Name)) : "N/A")
				.AddField("Genres", foundGame.Genres != null ? string.Join("\n", foundGame.Genres.Values.Take(3).Select(x => x.Name)) : "N/A")
				.AddField("Game Engine", foundGame.GameEngines != null ? foundGame.GameEngines.Values.First().Name : "N/A")
				.AddField("Developers", foundGame.InvolvedCompanies != null ? string.Join("\n", foundGame.InvolvedCompanies.Values.Take(3).Select(x => x.Company.Value.Name)) : "N/A")
				.AddField("Platforms", foundGame.Platforms != null ? string.Join("\n", foundGame.Platforms.Values.Take(3).Select(x => x.Name)) : "N/A")
				.WithFooter(foundGame.Summary != null ? foundGame.Summary.Length <= 350 ? foundGame.Summary : string.Concat(foundGame.Summary.AsSpan(0, 350), "...") : "No summary available")
				.Build();

			return gameEmbed;
		}

		[ComponentInteraction("game-*-*-*-*", true)]
		public async Task GameButtonAnswer(string action, string game, int count, ulong messageId)
		{
			switch (action)
			{
				case "forward":
					count += 1;
					break;
				case "backward":
					count -= 1;
					break;
			}

			await DeferAsync();

			Embed? gameEmbed = await GetGameEmbedAsync(game, count);
			if (gameEmbed == null)
			{
				await FollowupAsync("Mi dispiace, non ho trovato il gioco che stavi cercando");
				return;
			}

			EmbedField counter = gameEmbed.Fields.First(x => x.Name == "Risultato");
			string[] counterValues = counter.Value.Split(" di ");

			count = int.Parse(counterValues[0]) - 1;

			MessageComponent? messageComponent = new ComponentBuilder()
				.WithButton("<", $"game-backward-{game}-{count}-{messageId}")
				.WithButton(">", $"game-forward-{game}-{count}-{messageId}")
				.Build();

			await Context.Channel.ModifyMessageAsync(messageId, message =>
			{
				message.Embed = gameEmbed;
				message.Components = messageComponent;
			});
		}
	}

	[Group("random", "Generatore di cose random")]
	public class RandomGroup : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("numero", "Genero un numero random")]
		public async Task RandomNumber([Summary("minimo", "Minimo [0 default]")] int min = 0, [Summary("massimo", "Massimo [100 default]")] int max = 100,
			[Summary("ephemeral", "Voi vederlo solo tu?")]
			EAnswer ephemeral = EAnswer.No)
		{
			var randomNumber = Convert.ToString(new Random().Next(min, max));
			Embed embed = DiscordUtils.CreateEmbed(randomNumber);
			await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
		}

		[SlashCommand("dado", "Lancio uno o più dadi")]
		public async Task Dice([Summary("dadi", "Numero di dadi [default 1]")] int dices = 1, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
		{
			var dicesResults = "";
			for (var i = 0; i < dices; i++) dicesResults += Convert.ToString(new Random().Next(1, 7)) + " ";
			Embed embed = DiscordUtils.CreateEmbed(dicesResults);
			await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
		}

		[SlashCommand("moneta", "Lancio una moneta")]
		public async Task Coin([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
		{
			var list = new List<string> { "Testa", "Croce" };
			int index = new Random().Next(list.Count);
			string result = list[index];
			Embed embed = DiscordUtils.CreateEmbed(result);
			await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
		}

		[SlashCommand("opzione", "Scelgo un'opzione tra quelle che mi date")]
		public async Task RandomChoice([Summary("opzioni", "Opzioni separate dallo spazio")] string options, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
		{
			string[] separatedList = options.Split(" ");
			int index = new Random().Next(separatedList.Length);
			string result = separatedList[index];
			Embed embed = DiscordUtils.CreateEmbed(result);
			await RespondAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);
		}


		[SlashCommand("user", "Scelgo uno di voi")]
		public async Task OneOfYou([Summary("tutti", "Anche chi non è in chat vocale")] EAnswer all = EAnswer.No, [Summary("cortana", "Anche io?")] EAnswer cortana = EAnswer.No,
			[Summary("ephemeral", "Voi vederlo solo tu?")]
			EAnswer ephemeral = EAnswer.No)
		{
			IReadOnlyCollection<SocketGuildUser>? availableUsers = Context.Guild.Users;
			if (all == EAnswer.No)
				foreach (SocketVoiceChannel? channel in Context.Guild.VoiceChannels)
					if (channel.ConnectedUsers.Contains(Context.User))
						availableUsers = channel.ConnectedUsers;

			List<SocketGuildUser> users = availableUsers.Where(user => !user.IsBot || (user.IsBot && user.Id == DiscordUtils.Data.CortanaId && cortana == EAnswer.Si)).ToList();

			SocketGuildUser chosenUser = users[new Random().Next(0, users.Count)];
			await RespondAsync($"Ho scelto {chosenUser.Mention}", ephemeral: ephemeral == EAnswer.Si);
		}
	}

	[Group("gestione", "Comandi gestione server")]
	public class ManageGroup : InteractionModuleBase<SocketInteractionContext>
	{
		[SlashCommand("banned-words", "Vi mostro le parole bannate in questo server")]
		public async Task ShowBannedWords()
		{
			if (DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Count == 0)
			{
				await RespondAsync("Non ci sono parole vietate in questo server");
				return;
			}

			string bannedWordsList = DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Aggregate("Ecco le parole bannate di questo server:\n```\n", (current, word) => current + word + "\n");
			bannedWordsList += "```";
			await RespondAsync(bannedWordsList);
		}

		[SlashCommand("modify-banned-words", "Aggiungo o rimuovo parole bannate da questo server")]
		public async Task ShowBannedWords([Summary("action", "Cosa vuoi fare?")] EListAction action, [Summary("word", "Parola bannata")] string word)
		{
			word = word.ToLower();
			switch (action)
			{
				case EListAction.Crea:
					if (DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Contains(word))
					{
						await RespondAsync("Questa parola è già presente tra quelle bannate in questo server");
						return;
					}

					DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Add(word);
					await RespondAsync("Parola aggiunta con successo! Usa il seguente comando per visualizzare la nuova lista: ```/gestione banned-words```");
					break;
				case EListAction.Elimina:
					if (!DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Contains(word))
					{
						await RespondAsync("Questa parola non è presente tra quelle bannate in questo server");
						return;
					}

					DiscordUtils.GuildSettings[Context.Guild.Id].BannedWords.Remove(word);
					await RespondAsync("Parola rimossa con successo! Usa il seguente comando per visualizzare la nuova lista: ```/gestione banned-words```");
					break;
			}

			DiscordUtils.UpdateSettings();
		}

		[SlashCommand("kick", "Kicko un utente dal server")]
		public async Task KickMember([Summary("user", "Chi vuoi kickare?")] SocketGuildUser user, [Summary("motivazione", "Per quale motivo?")] string reason = "Motivazione non specificata")
		{
			if (user.Id == DiscordUtils.Data.ChiefId)
			{
				await RespondAsync("Non farei mai una cosa simile");
			}
			else if (user.Id == DiscordUtils.Data.CortanaId)
			{
				await RespondAsync("Divertente");
			}
			else
			{
				await user.KickAsync(reason);
				await RespondAsync("Utente kickato");
			}
		}

		[SlashCommand("ban", "Banno un utente dal server")]
		public async Task BanMember([Summary("user", "Chi vuoi bannare?")] SocketGuildUser user, [Summary("motivazione", "Per quale motivo?")] string reason = "Motivazione non specificata")
		{
			if (user.Id == DiscordUtils.Data.ChiefId)
			{
				await RespondAsync("Non farei mai una cosa simile");
			}
			else if (user.Id == DiscordUtils.Data.CortanaId)
			{
				await RespondAsync("Divertente");
			}
			else
			{
				await user.BanAsync(reason: reason);
				await RespondAsync("Utente bannato");
			}
		}

		[SlashCommand("imposta-timeout", "Timeout di un utente dal server")]
		public async Task SetTimeoutMember([Summary("user", "Chi vuoi in timeout?")] SocketGuildUser user,
			[Summary("tempo", "Quanti minuti deve durare il timeout? [Default: 10]")]
			double timeout = 10)
		{
			if (user.Id == DiscordUtils.Data.ChiefId)
			{
				await RespondAsync("Non farei mai una cosa simile");
			}
			else if (user.Id == DiscordUtils.Data.CortanaId)
			{
				await RespondAsync("Divertente");
			}
			else
			{
				await user.SetTimeOutAsync(TimeSpan.FromMinutes(timeout));
				await RespondAsync($"Utente in timeout per {timeout} minuti");
			}
		}

		[SlashCommand("rimuovi-timeout", "Rimuovo il timeout di un utente del server")]
		public async Task RemoveTimeoutMember([Summary("user", "Di chi vuoi rimuovere il timeout?")] SocketGuildUser user)
		{
			await user.RemoveTimeOutAsync();
			await RespondAsync("Timeout rimosso");
		}
	}
}