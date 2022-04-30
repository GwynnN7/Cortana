using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Iot.Device.CpuTemperature;
using System.Globalization;

namespace DiscordBot
{
    public class DiscordBot
    {
        public static void BootDiscordBot() => new DiscordBot().MainAsync().GetAwaiter().GetResult();

        public static DiscordSocketClient Cortana;
        public static System.Timers.Timer ActivityTimer = new System.Timers.Timer();
        public async Task MainAsync()
        {
            var client = new DiscordSocketClient(ConfigureSocket());
            var config = ConfigurationBuilder();
            var services = ConfigureServices(client);

            var commands = services.GetRequiredService<InteractionService>();

            await services.GetRequiredService<CommandHandler>().InitializeAsync();

            client.Log += LogAsync;
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
                ActivityTimer.Elapsed += new System.Timers.ElapsedEventHandler(ActivityTimerElased);
                ActivityTimer.Start();

                FindChannelToJoin(client.GetGuild(DiscordData.DiscordIDs.NoMenID));
            };

            await client.LoginAsync(TokenType.Bot, config["token"]);
            await client.StartAsync();

            await Task.Delay(Timeout.Infinite);
        }

        private static async void ActivityTimerElased(object? sender, System.Timers.ElapsedEventArgs e)
        {
            using CpuTemperature cpuTemperature = new CpuTemperature();
            var temperatures = cpuTemperature.ReadTemperatures();
            double average = 0;
            foreach (var temp in temperatures) average += temp.Temperature.DegreesCelsius;
            average /= temperatures.Count;
            Game Activity = new Game($"on Raspberry at {Math.Round(average, 1).ToString(CultureInfo.InvariantCulture)}°C", ActivityType.Playing);
            await Cortana.SetActivityAsync(Activity);
        }
        public static async Task Disconnect()
        {
            foreach(var guild in Modules.AudioHandler.AudioClients)
            {
                DiscordData.GuildSettings[guild.Key].AutoJoin = false;
                await Modules.AudioHandler.Play("ShutDown", guild.Key, Modules.AudioHandler.AudioIn.Local);
                await Task.Delay(1500);
                Modules.AudioHandler.Disconnect(guild.Key);          
            }
            await Task.Delay(1000);
            await Cortana.StopAsync();
            await Cortana.LogoutAsync();
        }

        void FindChannelToJoin(SocketGuild Guild)
        {
            if (!DiscordData.GuildSettings[Guild.Id].AutoJoin) return;
            //NOMEN-ONLY
            if (Guild.Id == DiscordData.DiscordIDs.NoMenID)
            {
                List<SocketVoiceChannel> AvailableChannel = new List<SocketVoiceChannel>();
                bool canMove = true;
                foreach (var voiceChannel in Guild.VoiceChannels)
                {
                    if (voiceChannel.Id == DiscordData.DiscordIDs.CortanaChannelID)
                    {
                        if (voiceChannel.Users.Count > 0 && !voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) canMove = false;
                        AvailableChannel.Add(voiceChannel);
                        continue;
                    }
                    if (voiceChannel.Users.Count > 0 && !voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) AvailableChannel.Add(voiceChannel);

                    else if (voiceChannel.Users.Count > 1 && voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) canMove = false;
                }
                if (AvailableChannel.Count > 1) AvailableChannel.Remove(Guild.GetVoiceChannel(DiscordData.DiscordIDs.CortanaChannelID));
                if (canMove) Modules.AudioHandler.JoinChannel(AvailableChannel[0]);
            }
        }

        async Task OnUserVoiceStateUpdate(SocketUser User, SocketVoiceState OldState, SocketVoiceState NewState)
        {
            //NOMEN-ONLY
            if (NewState.VoiceChannel != null && NewState.VoiceChannel.Id == DiscordData.DiscordIDs.CortanaChannelID && User.Id != DiscordData.DiscordIDs.CortanaID)
            {
                await NewState.VoiceChannel.Guild.GetUser(User.Id).ModifyAsync(x => x.ChannelId = NewState.VoiceChannel.Guild.VoiceChannels.FirstOrDefault()?.Id);
            }

            FindChannelToJoin((NewState.VoiceChannel ?? OldState.VoiceChannel).Guild);

            if (User.IsBot) return;

            var Guild = (OldState.VoiceChannel ?? NewState.VoiceChannel).Guild;

            if (OldState.VoiceChannel == null && NewState.VoiceChannel != null)
            {
                var DisplayName = NewState.VoiceChannel.Guild.GetUser(User.Id).DisplayName;
                string Title = $"{RequestsHandler.Functions.RandomOption(new string[]{ "Ciao", "Benvenuto", "Salve" })}  {DisplayName}";
                Embed embed = DiscordData.CreateEmbed(Title, WithoutAuthor: true, Footer: new EmbedFooterBuilder { IconUrl = User.GetAvatarUrl(), Text = "Sei entrato alle:" });

                await Guild.GetTextChannel(DiscordData.GuildSettings[Guild.Id].GreetingsChannel).SendMessageAsync(embed: embed);

                if (DiscordData.TimeConnected.ContainsKey(User.Id)) DiscordData.TimeConnected.Remove(User.Id);
                DiscordData.TimeConnected.Add(User.Id, DateTime.Now);

                await Task.Delay(2000);
                await Modules.AudioHandler.Play("Hello", NewState.VoiceChannel.Guild.Id, Modules.AudioHandler.AudioIn.Local);
            }
            else if (OldState.VoiceChannel != null && NewState.VoiceChannel == null)
            {
                var DisplayName = OldState.VoiceChannel.Guild.GetUser(User.Id).DisplayName;
                string Title = $"{RequestsHandler.Functions.RandomOption(new string[] { "Adios", "A Miaokai", "A dopo" })}  {DisplayName}";
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