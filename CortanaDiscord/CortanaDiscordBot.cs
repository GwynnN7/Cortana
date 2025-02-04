using CortanaDiscord.Handlers;
using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Timer = CortanaLib.Timer;

namespace CortanaDiscord;

public static class CortanaDiscordBot
{
	private static CancellationTokenSource? _token;
	public static async Task Main()
	{
		var client = new DiscordSocketClient(ConfigureSocket());
		ServiceProvider services = ConfigureServices(client);

		var commands = services.GetRequiredService<InteractionService>();

		await services.GetRequiredService<CommandHandler>().InitializeAsync();

		client.Log += LogAsync;
		client.MessageReceived += ClientMessageReceived;
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

			new Timer("discord-activity-timer", null, ActivityTimerElapsed, ETimerType.Utility, ETimerLoop.Interval).Set((30, 0, 0));

			foreach (SocketGuild? guild in DiscordUtils.Cortana.Guilds)
			{
				SocketVoiceChannel? channel = AudioHandler.GetAvailableChannel(guild);
				if (channel != null) AudioHandler.Connect(channel);
			}

			await DiscordUtils.SendToChannel<string>("I'm Online", ECortanaChannels.Cortana);
		};

		await client.LoginAsync(TokenType.Bot, DataHandler.Env("CORTANA_DISCORD_TOKEN"));
		await client.StartAsync();

		await Signals.WaitForInterrupt();
		await StopDiscordBot();
		Console.WriteLine("Discord Bot shut down");
	}
	
	private static async Task StopDiscordBot()
	{
		foreach ((ulong clientId, _) in AudioHandler.AudioClients)
		{
			DiscordUtils.GuildSettings[clientId].AutoJoin = false;
			AudioHandler.Disconnect(clientId);
		}

		await Task.Delay(1000);
		await DiscordUtils.Cortana.StopAsync();
	}

	private static async Task ClientMessageReceived(SocketMessage arg)
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

	private static async Task ActivityTimerElapsed(object? sender)
	{
		try
		{
			ResponseMessage response = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Temperature}");
			var activity = new Game($"on Raspberry at {response.Response}");
			await DiscordUtils.Cortana.SetActivityAsync(activity);
		}
		catch
		{
			DataHandler.Log("Discord", "Errore di connessione, impossibile aggiornare l'Activity Status");
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
		DataHandler.Log("Discord", message.Message);
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