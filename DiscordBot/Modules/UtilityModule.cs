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
            string result = Utility.HardwareDriver.ToggleLamp();
            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("room", "Accendi o spegni l'hardware fondamentale", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task BootUp([Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Procedo");
            await RespondAsync(embed: embed, ephemeral: true);

            Utility.HardwareDriver.ToggleLamp();
            Utility.HardwareDriver.SwitchPC(trigger);
            Utility.HardwareDriver.SwitchOLED(trigger);
            Utility.HardwareDriver.SwitchLED(trigger);
        }

        [SlashCommand("status", "Come sta andando sul raspberry")]
        [RequireOwner]
        public async Task Status()
        {
            var Image = await Utility.Functions.Screenshot();
            Embed embed = DiscordData.CreateEmbed(Title: "Status Report", Description: Utility.HardwareDriver.GetCPUTemperature());
            await RespondWithFileAsync(fileStream: Image, fileName: "Screenshot", embed: embed);
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

        [SlashCommand("hardware", "Interagisci con l'hardware in camera", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task HardwareInteract([Summary("dispositivo", "Con cosa vuoi interagire?")] EHardwareElements element, [Summary("azione", "Cosa vuoi fare?")] EHardwareTrigger trigger)
        {
            string result = element switch
            {
                EHardwareElements.Lamp => Utility.HardwareDriver.ToggleLamp(),
                EHardwareElements.PC => Utility.HardwareDriver.SwitchPC(trigger),
                EHardwareElements.OLED => Utility.HardwareDriver.SwitchOLED(trigger),
                EHardwareElements.LED => Utility.HardwareDriver.SwitchLED(trigger),
                EHardwareElements.Outlets => Utility.HardwareDriver.SwitchOutlets(trigger),
                _ => "Dispositivo hardware non presente"
            };

            Embed embed = DiscordData.CreateEmbed(Title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("shutdown", "Vado offline su discord")]
        [RequireOwner]
        public async Task Shutdown()
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Shutting Down Chief");
            await RespondAsync(embed: embed, ephemeral: true);

            await DiscordBot.Disconnect();
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
