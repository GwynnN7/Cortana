using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    public class UtilityModule : InteractionModuleBase<SocketInteractionContext>
    {
        public InteractionService Commands { get; set; }
        private CommandHandler handler;

        public UtilityModule(CommandHandler handler) => this.handler = handler;

        [SlashCommand("light", "Accendi o spegni la luce")]
        [RequireOwner]
        public async Task LightToggle()
        {
            string result = await RequestsHandler.MakeRequest.Execute(RequestsHandler.ERequestsType.Automation, "light-toggle");
            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("pc-power", "Accendi o spegni la luce")]
        [RequireOwner]
        public async Task PCPower([Summary("state", "Cosa vuoi fare?")][Choice("Accendi", "on")][Choice("Spegni", "off")][Choice("Toggle", "toggle")] string action)
        {
            string result = await RequestsHandler.MakeRequest.Execute(RequestsHandler.ERequestsType.Automation, "pc-power", $"state={action}");
            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
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
            var ImageStream = RequestsHandler.Functions.CreateQRCode(content, NormalColor == EAnswer.Si, QuietZones == EAnswer.Si);

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

        [Group("random", "Generatore di cose random")]
        public class RandomGroup : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("numero", "Genero un numero random")]
            public async Task RandomNumber([Summary("minimo", "Minimo [0 default]")] int min = 0, [Summary("massimo", "Massimo [100 default]")] int max = 100, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string randomNumber = RequestsHandler.Functions.RandomNumber(min, max);
                Embed embed = DiscordData.CreateEmbed(Title: randomNumber);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("dado", "Lancio uno o più dadi")]
            public async Task Dice([Summary("dadi", "Numero di dadi [default 1]")] int dices = 1, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string dicesResults = RequestsHandler.Functions.RandomDice(dices);
                Embed embed = DiscordData.CreateEmbed(Title: dicesResults);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("moneta", "Lancio una moneta")]
            public async Task Coin([Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string result = RequestsHandler.Functions.TOC();
                Embed embed = DiscordData.CreateEmbed(Title: result);
                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("opzione", "Scelgo un'opzione tra quelle che mi date")]
            public async Task RandomChoice([Summary("opzioni", "Opzioni separate dallo spazio")] string options, [Summary("ephemeral", "Voi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                string result = RequestsHandler.Functions.RandomOption(options);
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
                        if (channel.Users.Contains(Context.User)) AvailableUsers = channel.Users;
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
