using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot
{
    public class DiscordBot
    {
        public static void BootDiscordBot() => new DiscordBot().MainAsync().GetAwaiter().GetResult();

        private static List<string> Logs = new List<string>();
        public static DiscordSocketClient Cortana;
        public static System.Timers.Timer ActivityTimer = new System.Timers.Timer();
        public static System.Timers.Timer StatusTimer = new System.Timers.Timer();
        public async Task MainAsync()
        {
            var client = new DiscordSocketClient(ConfigureSocket());
            var config = ConfigurationBuilder();
            var services = ConfigureServices(client);

            var commands = services.GetRequiredService<InteractionService>();

            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            client.Log += LogAsync;
            client.Disconnected += Client_Disconnected;
            client.Connected += Client_Connected;
            client.UserVoiceStateUpdated += OnUserVoiceStateUpdate;
            client.JoinedGuild += OnServerJoin;
            client.LeftGuild += OnServerLeave;
            client.UserJoined += OnUserJoin;
            client.UserLeft += OnUserLeft;

            commands.Log += LogAsync;

            client.Ready += async () =>
            {
                Cortana = client;

                DiscordData.InitSettings(client.Guilds);
                DiscordData.LoadData(client.Guilds);
                DiscordData.CortanaUser = Cortana.CurrentUser;

                await commands.RegisterCommandsToGuildAsync(DiscordData.DiscordIDs.NoMenID, true);
                await commands.RegisterCommandsToGuildAsync(DiscordData.DiscordIDs.HomeID, true);
                //await commands.RegisterCommandsGloballyAsync(true);

                ActivityTimer.Interval = 10000;
                ActivityTimer.Elapsed += new System.Timers.ElapsedEventHandler(ActivityTimerElapsed);
                ActivityTimer.Start();

                StatusTimer.Interval = 8000000;
                StatusTimer.Elapsed += new System.Timers.ElapsedEventHandler(StatusTimerElapsed);
                StatusTimer.Start();

                var channel = Modules.AudioHandler.GetAvailableChannel(client.GetGuild(DiscordData.DiscordIDs.NoMenID));
                if (channel != null) Modules.AudioHandler.JoinChannel(channel);
            };

            await client.LoginAsync(TokenType.Bot, config["token"]);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task Client_Connected()
        {
            try
            {
                var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync("Connessione avventua");
            }
            catch
            {
                Console.WriteLine("Connessione avvenuta");
            }
        }

        private static async Task Client_Disconnected(Exception arg)
        {
            try
            {
                var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync($"Disconnessione avventua con il seguente errore: {arg.GetBaseException().ToString()}");
            }
            catch
            {
                Console.WriteLine($"Disconnessione avventua con il seguente errore: {arg.GetBaseException().ToString()}");
            }
        }

        private static async void ActivityTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            string Temp = Utility.HardwareDriver.GetCPUTemperature();
            Game Activity = new Game($"on Raspberry at {Temp}", ActivityType.Playing);
            await Cortana.SetActivityAsync(Activity);
        }

        private static async void StatusTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            Embed embed = DiscordData.CreateEmbed(Title: "Status Report", Description: Utility.HardwareDriver.GetCPUTemperature());
            var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
            string value = "";
            Logs.ForEach(log => value += log + "\n");
            await Chief.SendMessageAsync(text: value, embed: embed);
        }

        public static async Task Disconnect()
        {
            foreach(var guild in Modules.AudioHandler.AudioClients)
            {
                DiscordData.GuildSettings[guild.Key].AutoJoin = false;
                await Modules.AudioHandler.Play("Shutdown", guild.Key, EAudioSource.Local);
                await Task.Delay(1500);
                Modules.AudioHandler.Disconnect(guild.Key);          
            }
            await Task.Delay(1000);
            await Cortana.StopAsync();
            await Cortana.LogoutAsync();
        }

        async Task OnUserVoiceStateUpdate(SocketUser User, SocketVoiceState OldState, SocketVoiceState NewState)
        {
            var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
            await Chief.SendMessageAsync($"{User.Username} ha richiamato OnVoiceStateUpdate");

            var Guild = (OldState.VoiceChannel ?? NewState.VoiceChannel).Guild;

            //NOMEN-ONLY
            if (NewState.VoiceChannel != null && NewState.VoiceChannel.Id == DiscordData.DiscordIDs.CortanaChannelID && User.Id != DiscordData.DiscordIDs.CortanaID)
            {
                await NewState.VoiceChannel.Guild.GetUser(User.Id).ModifyAsync(x => x.ChannelId = NewState.VoiceChannel.Guild.VoiceChannels.FirstOrDefault()?.Id);
            }

            Modules.AudioHandler.TryConnection(Guild);

            if (User.Id == DiscordData.DiscordIDs.CortanaID) return;

            if (OldState.VoiceChannel == null && NewState.VoiceChannel != null)
            {
                var DisplayName = NewState.VoiceChannel.Guild.GetUser(User.Id).DisplayName;
                string Title = $"{Utility.Functions.RandomOption(new string[]{ "Ciao", "Benvenuto", "Salve" })}  {DisplayName}";
                Embed embed = DiscordData.CreateEmbed(Title, WithoutAuthor: true, Footer: new EmbedFooterBuilder { IconUrl = User.GetAvatarUrl(), Text = "Sei entrato alle:" });

                await Guild.GetTextChannel(DiscordData.GuildSettings[Guild.Id].GreetingsChannel).SendMessageAsync(embed: embed);

                if (DiscordData.TimeConnected.ContainsKey(User.Id)) DiscordData.TimeConnected.Remove(User.Id);
                DiscordData.TimeConnected.Add(User.Id, DateTime.Now);

                await Task.Delay(1500);
                await Modules.AudioHandler.Play("Hello", NewState.VoiceChannel.Guild.Id, EAudioSource.Local);
            }
            else if (OldState.VoiceChannel != null && NewState.VoiceChannel == null)
            {
                var DisplayName = OldState.VoiceChannel.Guild.GetUser(User.Id).DisplayName;
                string Title = $"{Utility.Functions.RandomOption(new string[] { "Adios", "A Miaokai", "A dopo" })}  {DisplayName}";
                Embed embed = DiscordData.CreateEmbed(Title, WithoutAuthor: true, Footer: new EmbedFooterBuilder { IconUrl = User.GetAvatarUrl(), Text = "Sei uscito alle:" });
                await Guild.GetTextChannel(DiscordData.GuildSettings[Guild.Id].GreetingsChannel).SendMessageAsync(embed: embed);

                if (DiscordData.TimeConnected.ContainsKey(User.Id)) DiscordData.TimeConnected.Remove(User.Id);
            }
        }

        async Task OnServerJoin(SocketGuild Guild)
        {
            DiscordData.AddGuildSettings(Guild);
            DiscordData.AddGuildUserData(Guild);

            await Guild.DefaultChannel.SendMessageAsync(embed: DiscordData.CreateEmbed("Ciao, sono Cortana"));
        }

        Task OnServerLeave(SocketGuild Guild)
        {
            if (DiscordData.GuildSettings.ContainsKey(Guild.Id)) DiscordData.GuildSettings.Remove(Guild.Id);
            if (DiscordData.UserGuildData.ContainsKey(Guild.Id)) DiscordData.UserGuildData.Remove(Guild.Id);
            return Task.CompletedTask;
        }

        async Task OnUserJoin(SocketGuildUser User)
        {
            if (User.IsBot) return;
            DiscordData.UserGuildData[User.Guild.Id].UserData.Add(User.Id, new GuildUserData());

            await User.Guild.GetTextChannel(DiscordData.GuildSettings[User.Guild.Id].GreetingsChannel).SendMessageAsync(embed: DiscordData.CreateEmbed($"Benvenuto {User.DisplayName}"));
        }

        Task OnUserLeft(SocketGuild Guild, SocketUser User)
        {
            if (!User.IsBot) DiscordData.UserGuildData[Guild.Id].UserData.Remove(User.Id);
            return Task.CompletedTask;
        }

        static Task LogAsync(LogMessage message)
        {
            Logs.Add(message.Message);
            Console.WriteLine("Log:" + message.Message);
            return Task.CompletedTask;
        }

        private DiscordSocketConfig ConfigureSocket()
        {
            return new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.All,
                AlwaysDownloadUsers = true,
                UseInteractionSnowflakeDate = false
            };
        }
        private ServiceProvider ConfigureServices(DiscordSocketClient client)
        {
            return new ServiceCollection()
                .AddSingleton(client)
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<CommandHandler>()
                .BuildServiceProvider();
        }

        private IConfigurationRoot ConfigurationBuilder()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Data/Discord/Token.json")
                .Build();
        }
    }
}