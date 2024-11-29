using System.Timers;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Modules;
using Microsoft.Extensions.DependencyInjection;
using Processor;
using Timer = Processor.Timer;

namespace DiscordBot;

public static class CortanaDiscordBot
{
	private static CancellationTokenSource? _token;
	public static async Task BootDiscordBot()
	{
		var client = new DiscordSocketClient(ConfigureSocket());
		ServiceProvider services = ConfigureServices(client);

		var commands = services.GetRequiredService<InteractionService>();

		await services.GetRequiredService<CommandHandler>().InitializeAsync();

		client.Log += LogAsync;
		client.MessageReceived += Client_MessageReceived;
		client.UserVoiceStateUpdated += OnUserVoiceStateUpdate;
		client.JoinedGuild += OnServerJoin;
		client.LeftGuild += OnServerLeave;
		client.UserJoined += OnUserJoin;
		client.UserLeft += OnUserLeft;

		commands.Log += LogAsync;

		client.Ready += async () =>
		{
			DiscordUtils.InitSettings(client);
			await commands.RegisterCommandsGloballyAsync();

			_ = new Timer("discord-activity-timer", null, (0, 0, 10), ActivityTimerElapsed, ETimerType.Utility, ETimerLoop.Interval);

			foreach (SocketGuild? guild in DiscordUtils.Cortana.Guilds)
			{
				SocketVoiceChannel? channel = AudioHandler.GetAvailableChannel(guild);
				if (channel != null) AudioHandler.Connect(channel);
			}

			DiscordUtils.SendToChannel("I'm Online", ECortanaChannels.Cortana);
		};

		await client.LoginAsync(TokenType.Bot, Software.Secrets.DiscordToken);
		await client.StartAsync();

		_token = new CancellationTokenSource();
		try
		{
			await Task.Delay(Timeout.Infinite, _token.Token);
		}
		catch (TaskCanceledException)
		{
			Console.WriteLine("Discord Bot shut down");
		}
	}
	
	public static async Task StopDiscordBot()
	{
		foreach ((ulong clientId, _) in AudioHandler.AudioClients)
		{
			DiscordUtils.GuildSettings[clientId].AutoJoin = false;
			AudioHandler.Disconnect(clientId);
		}

		await Task.Delay(1000);
		await DiscordUtils.Cortana.StopAsync();
		
		if(_token != null) await _token.CancelAsync();
	}

	private static async Task Client_MessageReceived(SocketMessage arg)
	{
		if (arg.Author.Id == DiscordUtils.Data.CortanaId) return;
		string message = arg.Content.ToLower();

		if (arg.Channel.GetChannelType() != ChannelType.DM)
			if (arg.Channel is SocketGuildChannel channel)
				if (DiscordUtils.GuildSettings[channel.Guild.Id].BannedWords.Any(word => message.Contains(word)))
				{
					await arg.Channel.SendMessageAsync("Ho trovato una parola non consentita, sono costretta ad eliminare il messaggio");
					await arg.DeleteAsync();
					return;
				}

		switch (message)
		{
			case "cortana":
				await arg.Channel.SendMessageAsync($"Dimmi {arg.Author.Mention}");
				break;
			case "ciao cortana":
				await arg.Channel.SendMessageAsync($"Ciao {arg.Author.Mention}");
				break;
		}
	}

	private static async void ActivityTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		try
		{
			string temp = Hardware.GetCpuTemperature();
			var activity = new Game($"on Raspberry at {temp}");
			await DiscordUtils.Cortana.SetActivityAsync(activity);
		}
		catch
		{
			Software.Log("Discord", "Errore di connessione, impossibile aggiornare l'Activity Status");
		}
	}

	private static async Task OnUserVoiceStateUpdate(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
	{
		SocketGuild? guild = (oldState.VoiceChannel ?? newState.VoiceChannel).Guild;

		AudioHandler.HandleConnection(guild);

		if (user.Id == DiscordUtils.Data.CortanaId) return;

		if (oldState.VoiceChannel == null && newState.VoiceChannel != null)
		{
			string? displayName = newState.VoiceChannel.Guild.GetUser(user.Id).DisplayName;
			var title = $"Ciao {displayName}";
			Embed embed = DiscordUtils.CreateEmbed(title, withoutAuthor: true, footer: new EmbedFooterBuilder { IconUrl = user.GetAvatarUrl(), Text = "Joined at:" });

			if (DiscordUtils.GuildSettings[guild.Id].Greetings) await guild.GetTextChannel(DiscordUtils.GuildSettings[guild.Id].GreetingsChannel).SendMessageAsync(embed: embed);

			DiscordUtils.TimeConnected.Remove(user.Id);
			DiscordUtils.TimeConnected.Add(user.Id, DateTime.Now);

			if (newState.VoiceChannel != AudioHandler.GetCurrentCortanaChannel(guild)) return;

			await Task.Delay(1000);
			await AudioHandler.SayHello(newState.VoiceChannel.Guild.Id);
		}
		else if (oldState.VoiceChannel != null && newState.VoiceChannel == null)
		{
			string? displayName = oldState.VoiceChannel.Guild.GetUser(user.Id).DisplayName;
			var title = $"A dopo {displayName}";
			Embed embed = DiscordUtils.CreateEmbed(title, withoutAuthor: true, footer: new EmbedFooterBuilder { IconUrl = user.GetAvatarUrl(), Text = "Left at:" });
			if (DiscordUtils.GuildSettings[guild.Id].Greetings) await guild.GetTextChannel(DiscordUtils.GuildSettings[guild.Id].GreetingsChannel).SendMessageAsync(embed: embed);

			DiscordUtils.TimeConnected.Remove(user.Id);
		}
	}

	private static async Task OnServerJoin(SocketGuild guild)
	{
		DiscordUtils.AddGuildSettings(guild);

		await guild.DefaultChannel.SendMessageAsync(embed: DiscordUtils.CreateEmbed("Ciao, sono Cortana"));
	}

	private static Task OnServerLeave(SocketGuild guild)
	{
		DiscordUtils.GuildSettings.Remove(guild.Id);
		DiscordUtils.UpdateSettings();
		return Task.CompletedTask;
	}

	private static async Task OnUserJoin(SocketGuildUser user)
	{
		if (user.IsBot) return;

		await user.Guild.GetTextChannel(DiscordUtils.GuildSettings[user.Guild.Id].GreetingsChannel).SendMessageAsync(embed: DiscordUtils.CreateEmbed($"Benvenuto {user.DisplayName}"));
	}

	private static Task OnUserLeft(SocketGuild guild, SocketUser user)
	{
		return Task.CompletedTask;
	}

	private static Task LogAsync(LogMessage message)
	{
		Software.Log("Discord", message.Message);
		return Task.CompletedTask;
	}

	private static DiscordSocketConfig ConfigureSocket()
	{
		return new DiscordSocketConfig
		{
			GatewayIntents = GatewayIntents.All,
			AlwaysDownloadUsers = true,
			UseInteractionSnowflakeDate = false
		};
	}

	private static ServiceProvider ConfigureServices(DiscordSocketClient client)
	{
		return new ServiceCollection()
			.AddSingleton(client)
			.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
			.AddSingleton<CommandHandler>()
			.BuildServiceProvider();
	}
}