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
            client.MessageReceived += Client_MessageReceived;
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
                DiscordData.Cortana = Cortana;

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

                var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync("I'm ready Chief!");
                Console.WriteLine("I'm ready Chief");

                Utility.EmailHandler.Callbacks.Add(ReceiveMail);
            };

            await client.LoginAsync(TokenType.Bot, config["token"]);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private async Task Client_MessageReceived(SocketMessage arg)
        {
            var message = arg.Content.ToLower();

            if (arg.Channel.GetChannelType() != ChannelType.DM)
            {
                List<string> BannedWords = new() {"yuumi", "yummi", "seraphine", "teemo", "fortnite"};
                foreach(string word in BannedWords)
                {
                    if (message.Contains(word))
                    {
                        await arg.Channel.SendMessageAsync("Ho trovato una parola non consentita e sono costretta ad eliminare il messaggio");
                        await arg.DeleteAsync();
                        return;
                    }
                }
            }

            if (message == "cortana") await arg.Channel.SendMessageAsync($"Dimmi {arg.Author.Mention}");
            else if(message == "ciao cortana") await arg.Channel.SendMessageAsync($"Ciao {arg.Author.Mention}");
        }

        public static async Task ReceiveMail(Utility.UnreadEmailStructure email)
        {
            if (!email.Email.Log) return;
            var embed = DiscordData.CreateEmbed("Hai ricevuto una mail", Description: string.Format("[{0}](https://mail.google.com/mail/u/{1}/#inbox)", email.Email.Email, email.Email.Id));
            embed = embed.ToEmbedBuilder()
                .AddField("Oggetto", email.Subject)
                .AddField("Testo", email.Content)
                .AddField($"Da {email.FromName}", email.FromAddress)
                .Build();
            var Chief = await DiscordData.Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
            await Chief.SendMessageAsync(embed: embed);
        }

        private static Task Client_Connected()
        {
            Console.WriteLine($"Connessione avvenuta alle {DateTime.Now}");
            return Task.CompletedTask;
        }

        private static Task Client_Disconnected(Exception arg)
        {
            Console.WriteLine($"Disconnessione avventua alle {DateTime.Now} con il seguente errore: {arg.GetBaseException()}");
            return Task.CompletedTask;
        }

        private static async void ActivityTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                string Temp = Utility.HardwareDriver.GetCPUTemperature();
                Game Activity = new Game($"on Raspberry at {Temp}", ActivityType.Playing);
                await Cortana.SetActivityAsync(Activity);
            }
            catch
            {
                Console.WriteLine($"Non riesco a leggere la temperatura: {DateTime.Now}");
            }
            
        }

        private static async void StatusTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                Embed embed = DiscordData.CreateEmbed(Title: "Still alive Chief", Description: Utility.HardwareDriver.GetCPUTemperature());
                var Chief = await Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync(embed: embed);
            }
            catch
            {
                Console.WriteLine($"Non riesco a leggere la temperatura: {DateTime.Now}");
            }
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

            ActivityTimer.Stop();
            ActivityTimer.Close();

            StatusTimer.Stop();
            StatusTimer.Close();

            await Task.Delay(1000);
            await Cortana.StopAsync();
            await Cortana.LogoutAsync();
        }

        async Task OnUserVoiceStateUpdate(SocketUser User, SocketVoiceState OldState, SocketVoiceState NewState)
        {
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

                await Task.Delay(1000);
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