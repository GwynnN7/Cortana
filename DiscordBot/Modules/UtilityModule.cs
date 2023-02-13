using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using IGDB;
using IGDB.Models;
using System.Linq.Expressions;

namespace DiscordBot.Modules
{
    public class UtilityModule : InteractionModuleBase<SocketInteractionContext>
    {
        [Group("personal", "Comandi personali")]
        public class PersonalGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("avatar", "Vi mando la vostra immagine profile")]
            public async Task GetAvatar([Summary("user", "Di chi vuoi l'immagine?")] SocketUser user, [Summary("grandezza", "Grandezza dell'immagine [Da 64px a 4096px, inserisci un numero da 1 a 7]"), MaxValue(7), MinValue(1)] int size = 4)
            {
                var url = user.GetAvatarUrl(size: Convert.ToUInt16(Math.Pow(2, size + 5)));
                Embed embed = DiscordData.CreateEmbed("Profile Picture", user);
                embed = embed.ToEmbedBuilder().WithImageUrl(url).Build();
                await RespondAsync(embed: embed);
            }

            [SlashCommand("progetti", "Vi mando il link di Notion, per gestire i vostri progetti")]
            public async Task GetProjects()
            {
                Embed NotionEmbed = DiscordData.CreateEmbed("Progetti");
                NotionEmbed = NotionEmbed.ToEmbedBuilder()
                    .AddField("Notion", "[Vai a Notion](https://www.notion.so)")
                    .Build();
                await RespondAsync(embed: NotionEmbed);
            }
        }

        [Group("utility", "Comandi di utilità")]
        public class UtilityGroup : InteractionModuleBase<SocketInteractionContext>
        {

            [SlashCommand("my-code", "Vi mando il mio codice")]
            public async Task SendMyCode([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                Embed GithubEmbed = DiscordData.CreateEmbed("Github");
                GithubEmbed = GithubEmbed.ToEmbedBuilder()
                    .AddField("Cortana", "[Vai al codice](https://github.com/GwynbleiddN7/Cortana)")
                    .Build();
                await RespondAsync(embed: GithubEmbed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("ping", "Pinga un IP", runMode: RunMode.Async)]
            public async Task Ping([Summary("ip", "IP da pingare")] string ip)
            {
                bool result;
                if (ip == "pc") result = Utility.HardwareDriver.PingPC();
                else result = Utility.HardwareDriver.Ping(ip);

                if (result) await RespondAsync($"L'IP {ip} ha risposto al ping");
                else await RespondAsync($"L'IP {ip} non ha risposto al ping");
            }

            [SlashCommand("tempo-in-voicechat", "Da quanto tempo state in chat vocale?")]
            public async Task TimeConnected([Summary("user", "A chi è rivolto?")] SocketUser? User = null, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                if (User == null) User = Context.User;
                if (DiscordData.TimeConnected.ContainsKey(User.Id))
                {
                    var DeltaTime = DateTime.Now.Subtract(DiscordData.TimeConnected[User.Id]);
                    string ConnectionTime = "Sei connesso da";
                    if (DeltaTime.Hours > 0) ConnectionTime += $" {DeltaTime.Hours} ore";
                    if (DeltaTime.Minutes > 0) ConnectionTime += $" {DeltaTime.Minutes} minuti";
                    if (DeltaTime.Seconds > 0) ConnectionTime += $" {DeltaTime.Seconds} secondi";

                    var embed = DiscordData.CreateEmbed(Title: ConnectionTime, User: User);
                    await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
                }
                else
                {
                    var embed = DiscordData.CreateEmbed(Title: "Non connesso alla chat vocale", User: User);
                    await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
                }
            }

            [SlashCommand("qrcode", "Creo un QRCode con quello che mi dite")]
            public async Task CreateQR([Summary("contenuto", "Cosa vuoi metterci?")] string content, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No, [Summary("colore-base", "Vuoi il colore bianco normale?")] EAnswer NormalColor = EAnswer.No, [Summary("bordo", "Vuoi aggiungere il bordo?")] EAnswer QuietZones = EAnswer.Si)
            {
                var ImageStream = Utility.Functions.CreateQRCode(content, NormalColor == EAnswer.Si, QuietZones == EAnswer.Si);

                await RespondWithFileAsync(fileStream: ImageStream, fileName: "QRCode.png", ephemeral: Ephemeral == EAnswer.Si);
            }


            [SlashCommand("conta-parole", "Scrivi un messaggio e ti dirò quante parole e caratteri ci sono")]
            public async Task CountWorld([Summary("contenuto", "Cosa vuoi metterci?")] string content, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                Embed embed = DiscordData.CreateEmbed("Conta Parole");
                embed = embed.ToEmbedBuilder()
                    .AddField("Parole", content.Split(" ").Length)
                    .AddField("Caratteri", content.Replace(" ", "").Length)
                    .AddField("Caratteri con spazi", content.Length)
                    .Build();

                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
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
            public async Task WriteSomethingInDM([Summary("testo", "Cosa vuoi che dica?")] string text, [Summary("user", "Vuoi mandarlo in privato a qualcuno?")] SocketUser user)
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

            [SlashCommand("code", "Converto un messaggio sotto forma di codice")]
            public async Task ToCode()
            {
                await RespondWithModalAsync<CodeModal>("to-code");
            }

            public class CodeModal : IModal
            {
                public string Title => "Codice";

                [InputLabel("Cosa vuoi convertire?")]
                [ModalTextInput("text", TextInputStyle.Paragraph, placeholder: "Scrivi qui...")]
                public string Text { get; set; }
            }

            [ModalInteraction("to-code", true)]
            public async Task CodeModalResponse(CodeModal modal)
            {
                string text = modal.Text;
                if (text.Length >= 1500)
                {
                    await RespondAsync("```" + text.Substring(0, 1000) + "```");
                    await Context.Channel.SendMessageAsync("```" + text.Substring(1000) + "```");
                }
                else await RespondAsync("```" + text + "```");
            }

            [SlashCommand("turno", "Vi dico a chi tocca scegliere")]
            public async Task Turn()
            {
                if (Context.Guild.Id != DiscordData.DiscordIDs.NoMenID)
                {
                    await RespondAsync("Questo server non può usare questo comando");
                    return;
                }
                SocketGuildUser user = Context.Guild.GetUser(DiscordData.DiscordIDs.CortanaID);
                var today = DateTime.Today.DayOfWeek;
                switch (today)
                {
                    case DayOfWeek.Monday:
                    case DayOfWeek.Thursday:
                        user = Context.Guild.GetUser(DiscordData.DiscordIDs.ChiefID);
                        break;
                    case DayOfWeek.Tuesday:
                    case DayOfWeek.Friday:
                        user = Context.Guild.GetUser(306402234135085067);
                        break;
                    case DayOfWeek.Wednesday:
                    case DayOfWeek.Saturday:
                        user = Context.Guild.GetUser(648939655579828226);
                        break;
                    default:
                        break;
                }
                await RespondAsync($"Oggi tocca a {user.Mention}");
            }

            [SlashCommand("links", "Vi mando dei link utili")]
            public async Task SendLinks([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                Embed ShortcutsEmbed = DiscordData.CreateEmbed("Shorcuts");
                ShortcutsEmbed = ShortcutsEmbed.ToEmbedBuilder()
                    .AddField("Google", "[Vai al sito](https://www.google.com)", inline: true)
                    .AddField("Youtube", "[Vai al sito](https://youtube.com)", inline: true)
                    .AddField("Reddit", "[Vai al sito](https://www.reddit.com)", inline: true)
                    .AddField("Twitch", "[Vai al sito](http://www.twitch.tv)", inline: true)
                    .AddField("Instagram", "[Vai al sito](http://www.instagram.com)", inline: true)
                    .AddField("Twitter", "[Vai al sito](https://www.twitter.com)", inline: true)
                    .AddField("Pinterest", "[Vai al sito](https://www.pinterest.com)", inline: true)
                    .AddField("Deviantart", "[Vai al sito](https://www.deviantart.com)", inline: true)
                    .AddField("Artstation", "[Vai al sito](https://www.artstation.com)", inline: true)
                    .AddField("Speedtest", "[Vai al sito](https://www.speedtest.net/it)", inline: true)
                    .AddField("Google Drive", "[Vai al sito](https://drive.google.com)", inline: true)
                    .AddField("Gmail", "[Vai al sito](https://mail.google.com)", inline: true)
                    .Build();
                await RespondAsync(embed: ShortcutsEmbed, ephemeral: Ephemeral == EAnswer.Si);
            }
        }

        [Group("gestione", "Comandi gestione server")]
        public class ManageGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("kick", "Kicko un utente dal server")]
            public async Task KickMember([Summary("user", "Chi vuoi kickare?")] SocketGuildUser user, [Summary("motivazione", "Per quale motivo?")] string reason = "Motivazione non specificata")
            {
                if (user.Id == DiscordData.DiscordIDs.ChiefID) await RespondAsync("Non farei mai una cosa simile");
                else if (user.Id == DiscordData.DiscordIDs.CortanaID) await RespondAsync("Divertente");
                else
                {
                    await user.KickAsync(reason: reason);
                    await RespondAsync("Utente kickato");
                }
            }

            [SlashCommand("ban", "Banno un utente dal server")]
            public async Task BanMember([Summary("user", "Chi vuoi bannare?")] SocketGuildUser user, [Summary("motivazione", "Per quale motivo?")] string reason = "Motivazione non specificata")
            {
                if (user.Id == DiscordData.DiscordIDs.ChiefID) await RespondAsync("Non farei mai una cosa simile");
                else if (user.Id == DiscordData.DiscordIDs.CortanaID) await RespondAsync("Divertente");
                else
                {
                    await user.BanAsync(reason: reason);
                    await RespondAsync("Utente bannato");
                }
            }

            [SlashCommand("imposta-timeout", "Timeouto un utente dal server")]
            public async Task SetTimeoutMember([Summary("user", "Chi vuoi timeoutare?")] SocketGuildUser user, [Summary("tempo", "Quanti minuti deve durare il timeout? [Default: 10]")] double timeout = 10)
            {
                if (user.Id == DiscordData.DiscordIDs.ChiefID) await RespondAsync("Non farei mai una cosa simile");
                else if (user.Id == DiscordData.DiscordIDs.CortanaID) await RespondAsync("Divertente");
                else
                {
                    await user.SetTimeOutAsync(TimeSpan.FromMinutes(timeout));
                    await RespondAsync($"Utente timeoutato per {timeout} minuti");
                }
            }

            [SlashCommand("rimuovi-timeout", "Rimuovo il timeout di un utente del server")]
            public async Task RemoveTimeoutMember([Summary("user", "Di chi vuoi rimuovere il timeout?")] SocketGuildUser user)
            {
                await user.RemoveTimeOutAsync();
                await RespondAsync("Timeout rimosso");
            }
        }

        [Group("random", "Generatore di cose random")]
        public class RandomGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("numero", "Genero un numero random")]
            public async Task RandomNumber([Summary("minimo", "Minimo [0 default]")] int min = 0, [Summary("massimo", "Massimo [100 default]")] int max = 100, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string randomNumber = Utility.Functions.RandomNumber(min, max);
                Embed embed = DiscordData.CreateEmbed(Title: randomNumber);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("dado", "Lancio uno o più dadi")]
            public async Task Dice([Summary("dadi", "Numero di dadi [default 1]")] int dices = 1, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string dicesResults = Utility.Functions.RandomDice(dices);
                Embed embed = DiscordData.CreateEmbed(Title: dicesResults);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("moneta", "Lancio una moneta")]
            public async Task Coin([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string result = Utility.Functions.TOC();
                Embed embed = DiscordData.CreateEmbed(Title: result);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("opzione", "Scelgo un'opzione tra quelle che mi date")]
            public async Task RandomChoice([Summary("opzioni", "Opzioni separate dallo spazio")] string options, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string result = Utility.Functions.RandomOption(options);
                Embed embed = DiscordData.CreateEmbed(Title: result);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }


            [SlashCommand("user", "Scelgo uno di voi")]
            public async Task OneOfYou([Summary("tutti", "Anche chi non è in chat vocale")] EAnswer all = EAnswer.No, [Summary("cortana", "Anche io?")] EAnswer cortana = EAnswer.No, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                var Users = new List<SocketGuildUser>();
                var AvailableUsers = Context.Guild.Users;
                if (all == EAnswer.No)
                {
                    foreach (var channel in Context.Guild.VoiceChannels)
                    {
                        if (channel.ConnectedUsers.Contains(Context.User)) AvailableUsers = channel.ConnectedUsers;
                    }
                }
                foreach (var user in AvailableUsers)
                {
                    if (!user.IsBot || (user.IsBot && user.Id == DiscordData.DiscordIDs.CortanaID && cortana == EAnswer.Si)) Users.Add(user);
                }

                SocketGuildUser ChosenUser = Users[new Random().Next(0, Users.Count)];
                await RespondAsync($"Ho scelto {ChosenUser.Mention}", ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("lane", "Vi dico in che lane giocare")]
            public async Task Lane([Summary("user", "A chi è rivolto?")] SocketUser? User = null, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                if (User == null) User = Context.User;
                string[] lanes = new[] { "Top", "Jungle", "Mid", "ADC", "Support" };
                int randomIndex = new Random().Next(0, lanes.Length);
                await RespondAsync($"{User.Mention} vai *{lanes[randomIndex]}*", ephemeral: Ephemeral == EAnswer.Si);
            }
        }

        [Group("unipi", "Università")]
        public class UniversityGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("siti", "Siti UNIPI")]
            public async Task UnipiSites([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                Embed embed = DiscordData.CreateEmbed("Siti UNIPI", User: Context.User);
                EmbedBuilder embed_builder = embed.ToEmbedBuilder();
                embed_builder.AddField("Agenda Didattica", "[Vai al sito](https://agendadidattica.unipi.it/)");
                embed_builder.AddField("Università di Pisa", "[Vai al sito](https://www.unipi.it/)");
                embed_builder.AddField("Laurea in Informatica", "[Vai al sito](https://didattica.di.unipi.it/laurea-in-informatica/)");
                embed_builder.AddField("Area Personale", "[Vai al sito](https://www.studenti.unipi.it/)");
                embed_builder.AddField("CISA TOLC", "[Vai al sito](https://www.cisiaonline.it/)");

                Dictionary<string, ulong> ids = new Dictionary<string, ulong>()
                {
                    { "matteo", 468399905023721481 },
                    { "samuele", 648939655579828226 },
                    { "danu", 306402234135085067 }
                };

                if (Context.User.Id == ids["matteo"])
                {
                    embed_builder.AddField("Matricola", "658274");
                    embed_builder.AddField("Email", "m.cherubini6@studenti.unipi.it");
                }
                else if (Context.User.Id == ids["samuele"])
                {
                    embed_builder.AddField("Matricola", "658988");
                    embed_builder.AddField("Email", "s.baffo@studenti.unipi.it");
                }
                else if (Context.User.Id == ids["danu"])
                {
                    embed_builder.AddField("Matricola", "658992");
                    embed_builder.AddField("Email", "v.nitu@studenti.unipi.it");
                }
                else
                {
                    await RespondAsync("Mi dispiace, non ho dati su di te per questa università", ephemeral: Ephemeral == EAnswer.Si);
                    return;
                }

                await RespondAsync(embed: embed_builder.Build(), ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("corsi", "Lezioni UNIPI")]
            public async Task UnipiLessons([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                Dictionary<string, ulong> ids = new Dictionary<string, ulong>()
                {
                    { "matteo", 468399905023721481 },
                    { "samuele", 648939655579828226 },
                    { "danu", 306402234135085067 }
                };
                if (Context.User.Id != ids["matteo"] && Context.User.Id != ids["samuele"] && Context.User.Id != ids["danu"])
                {
                    await RespondAsync("Mi dispiace, non ho dati su di te per questa università", ephemeral: Ephemeral == EAnswer.Si);
                    return;
                }

                Embed embed = DiscordData.CreateEmbed("Lezioni UNIPI", User: Context.User);
                EmbedBuilder embed_builder = embed.ToEmbedBuilder();
                embed_builder.WithDescription("[Telegraph](https://telegra.ph/Informatica-CorsoA-22-23-09-15)\n[Calendario](https://agendadidattica.unipi.it/Prod/Home/Calendar)");
                embed_builder.AddField("Analisi", "[Classroom 2022/23](https://classroom.google.com/u/2/c/NDg5NzMwNTM2MjU2)\n[Classroom 2021/22](https://classroom.google.com/u/2/c/Mzg4NTMyMTcwNjA4)\n[SAI Evo](https://evo.di.unipi.it/student/courses/2)");
                embed_builder.AddField("Algebra Lineare", "[E-Learning](https://elearning.di.unipi.it/course/view.php?id=331)");
                embed_builder.AddField("Programmazione e Algoritmica", "[Classroom](https://classroom.google.com/u/2/c/NDg5NzMxMzU4ODAx)\n[SAI Evo](https://evo.di.unipi.it/student/courses/7)");
                embed_builder.AddField("Laboratorio 1", "[Classroom](https://classroom.google.com/u/2/c/NDg5NzMwNTM2Mjg4)\n[SAI Evo](https://evo.di.unipi.it/student/courses/8)");
                embed_builder.WithFooter("Corso A");

                await RespondAsync(embed: embed_builder.Build(), ephemeral: Ephemeral == EAnswer.Si);
            }
        }

        [Group("games", "videogames")]
        public class VideogamesGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("igdb", "Cerco uno o più giochi su IGDB")]
            public async Task SearchGame([Summary("game", "Nome del gioco")] string game, [Summary("count", "Numero di risultati")] int count = 1, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                var igdb = new IGDBClient("736igv8svzbet95taada229tyjak5s", "87bifi0v86jxfig2jm3to9m5y9lvza");
                string fields = "cover.image_id, game_engines.name, genres.name, involved_companies.company.name, name, platforms.name, rating, release_dates.human, summary, themes.name, url";
                string query = $"fields {fields}; search \"{game}\"; where category != (1,2,5,6,7,12); limit {count};";
                var games = await igdb.QueryAsync<IGDB.Models.Game>(IGDBClient.Endpoints.Games, query: query);
                foreach (var foundGame in games)
                {
                    Embed GameEmbed = DiscordData.CreateEmbed(foundGame.Name);
                    GameEmbed = GameEmbed.ToEmbedBuilder()
                        .WithDescription($"[Vai alla pagina IGDB]({foundGame.Url})")
                        .WithThumbnailUrl($"https://images.igdb.com/igdb/image/upload/t_cover_big/{foundGame.Cover.Value.ImageId}.jpg")
                        //.AddField("Game Engine", foundGame.GameEngines.Values.First().Name)
                        .AddField("Genres", string.Join("\n", foundGame.Genres.Values.Take(3).Select(x => x.Name)))
                        .AddField("Developers", string.Join("\n", foundGame.InvolvedCompanies.Values.Take(3).Select(x => x.Company.Value.Name)))
                        .AddField("Platforms", string.Join("\n", foundGame.Platforms.Values.Take(3).Select(x => x.Name)))
                        .AddField("Rating", foundGame.Rating != null ? foundGame.Rating.ToString() : "Unknown")
                        .AddField("Release Date", foundGame.ReleaseDates.Values.First().Human)
                        .AddField("Themes", string.Join("\n", foundGame.Themes.Values.Take(3).Select(x => x.Name)))
                        .WithFooter(foundGame.Summary.Substring(0, 350) + "...")
                        .Build();
                    await RespondAsync(embed: GameEmbed, ephemeral: Ephemeral == EAnswer.Si);

                }
            }
        }
    }
}
