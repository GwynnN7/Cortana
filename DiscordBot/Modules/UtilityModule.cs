using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    public class UtilityModule : InteractionModuleBase<SocketInteractionContext>
    {
        [Group("utility", "Generatore di cose random")]
        public class UtilityGroup : InteractionModuleBase<SocketInteractionContext>
        {
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

            [SlashCommand("avatar", "Vi mando la vostra immagine profile")]
            public async Task GetAvatar([Summary("user", "Di chi vuoi l'immagine?")] SocketUser user, [Summary("grandezza", "Grandezza dell'immagine [Da 64px a 4096px, inserisci un numero da 1 a 7]"), MaxValue(7), MinValue(1)] int size = 4)
            {
                var url = user.GetAvatarUrl(size: Convert.ToUInt16(Math.Pow(2, size + 5)));
                Embed embed = DiscordData.CreateEmbed("Profile Picture", user);
                embed = embed.ToEmbedBuilder().WithImageUrl(url).Build();
                await RespondAsync(embed: embed);
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

            [SlashCommand("turno", "Vi dico a chi tocca scegliere")]
            public async Task Turn()
            {
                if(Context.Guild.Id != DiscordData.DiscordIDs.NoMenID)
                {
                    await RespondAsync("Questo server non può usare questo comando");
                    return;
                }
                SocketGuildUser user = Context.Guild.GetUser(DiscordData.DiscordIDs.CortanaID);
                var today = DateTime.Today.DayOfWeek;
                switch(today)
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

            [SlashCommand("code", "Conveto un messaggio sotto forma di codice")]
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
                    await Context.Channel.SendMessageAsync("```"+ text.Substring(1000) + "```");
                }
                else await RespondAsync("```" + text + "```");
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

            [SlashCommand("kick", "Kicko un utente dal server")]
            public async Task KickMember([Summary("user", "Chi vuoi kickare?")] SocketGuildUser user)
            {
                if (user.Id == DiscordData.DiscordIDs.ChiefID) await RespondAsync("Non farei mai una cosa simile");
                else if (user.Id == DiscordData.DiscordIDs.CortanaID) await RespondAsync("Divertente");
                else 
                {
                    await user.KickAsync();
                    await RespondAsync("Utente kickato");
                }
            }

            [SlashCommand("progetti", "Gestione dei vostri progetti")]
            public async Task GetProjects()
            {
                if (!DiscordData.Projects.ContainsKey(Context.User.Id)) return;
                EmbedBuilder ProjectsEmbed = DiscordData.CreateEmbed("Progetti", User: Context.User).ToEmbedBuilder();

                List<SelectMenuOptionBuilder> projects = new();
                foreach(var proj in DiscordData.Projects[Context.User.Id].UserProjects)
                {
                    projects.Add(new SelectMenuOptionBuilder()
                        .WithLabel(proj.Key)
                        .WithValue(proj.Key));
                    ProjectsEmbed.AddField(proj.Key, proj.Value.Description);
                }

                var ComponentsBuilder = new ComponentBuilder().WithButton("Aggiungi", "add-project", ButtonStyle.Success, row: 1);

                if (projects.Count > 0) ComponentsBuilder.WithSelectMenu(customId: "project-list", projects, placeholder: "Seleziona un progetto", row: 0);

                await RespondAsync(embed: ProjectsEmbed.Build(), components: ComponentsBuilder.Build());
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
                    if(!user.IsBot || (user.IsBot  && user.Id == DiscordData.DiscordIDs.CortanaID && cortana == EAnswer.Si)) Users.Add(user);
                }
                
                SocketGuildUser ChosenUser = Users[new Random().Next(0, Users.Count)];
                await RespondAsync($"Ho scelto {ChosenUser.Mention}", ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("lane", "Vi dico in che lane giocare")]
            public async Task Lane([Summary("user", "A chi è rivolto?")] SocketUser? User = null, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                if (User == null) User = Context.User;
                string[] lanes = new[] {"Top", "Jungle", "Mid", "ADC", "Support"};
                int randomIndex = new Random().Next(0, lanes.Length);
                await RespondAsync($"{User.Mention} vai *{lanes[randomIndex]}*", ephemeral: Ephemeral == EAnswer.Si);
            }
        }
    }
}
