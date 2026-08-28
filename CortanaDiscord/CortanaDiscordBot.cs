using System.Collections.Concurrent;
using System.Globalization;
using CortanaDiscord.Handlers;
using CortanaDiscord.Utility;
using CortanaDiscord.Voice;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaDiscord;

public static class CortanaDiscordBot
{
	private static readonly bool DaveEnabled =
		!(DataHandler.EnvOrNull("CORTANA_DISCORD_DAVE")?.Equals("false", StringComparison.OrdinalIgnoreCase) ?? false);

	private static readonly TimeSpan WindowCacheFor = TimeSpan.FromSeconds(60);
	private static readonly ConcurrentDictionary<ulong, DateTime> Sessions = new();

	private static TimeSpan _chatWindow = TimeSpan.FromMinutes(1);
	private static DateTime _windowChecked = DateTime.MinValue;

	private static async Task<TimeSpan> ChatWindow()
	{
		if (DateTime.Now - _windowChecked < WindowCacheFor) return _chatWindow;

		_windowChecked = DateTime.Now;
		string value = await ApiHandler.Get($"{ERoute.AI}/settings/{EAiSetting.DiscordMinutes}");

		if (double.TryParse(value.Trim(), CultureInfo.InvariantCulture, out double minutes) && minutes > 0)
			_chatWindow = TimeSpan.FromMinutes(minutes);

		return _chatWindow;
	}

	public static async Task Main()
	{
		DataHandler.LoadEnvironment(required: false);

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
		client.Ready += OnReady;

		commands.Log += LogAsync;

		await client.LoginAsync(TokenType.Bot, DataHandler.Env("CORTANA_DISCORD_TOKEN"));
		await client.StartAsync();

		await SignalHandler.WaitForInterrupt();
		await StopDiscordBot();
		await DiscordUtils.Shutdown();
		await services.DisposeAsync();
		DataHandler.Log("Discord Bot Offline");

		Task OnReady()
		{
			_ = Task.Run(ReadyAsync);
			return Task.CompletedTask;
		}

		async Task ReadyAsync()
		{
			DiscordUtils.InitSettings(client);
			await commands.RegisterCommandsGloballyAsync();

			new Timer("discord-activity-timer", null, ActivityTimerElapsed, ETimerType.Utility, ETimerLoop.Interval).Set((30, 0, 0));

			foreach (SocketGuild guild in client.Guilds)
			{
				if (!DiscordUtils.TryGetGuildSettings(guild.Id, out GuildSettings? settings) || !settings.AutoJoin) continue;

				SocketVoiceChannel? channel = VoiceService.GetAvailableChannel(guild);
				if (channel != null) await VoiceService.Connect(channel);
			}

			await DiscordUtils.SendToChannel("I'm Online", ECortanaChannels.Cortana);
			DataHandler.Log("Discord Bot Online");
		}
	}

	private static async Task StopDiscordBot()
	{
		await VoiceService.DisconnectAll();
		await DiscordUtils.Cortana.StopAsync();
	}

	private static async Task ClientMessageReceived(SocketMessage arg)
	{
		if (arg.Author.Id == DiscordUtils.Data.CortanaId) return;
		string message = arg.Content.ToLower();

		if (arg.Channel.GetChannelType() != ChannelType.DM && arg.Channel is SocketGuildChannel channel)
		{
			if (DiscordUtils.TryGetGuildSettings(channel.Guild.Id, out GuildSettings? settings) &&
				settings.BannedWords.Any(word => message.Contains(word)))
			{
				await arg.Channel.SendMessageAsync("That word is not allowed here, deleting the message");
				await arg.DeleteAsync();
				return;
			}
		}

		if (arg is SocketUserMessage userMessage)
		{
			bool mentioned = MentionsCortana(userMessage);
			if (mentioned || SessionOpen(arg.Channel.Id))
			{
				await ReplyWithLlm(arg, userMessage, mentioned);
				return;
			}
		}

		switch (message)
		{
			case "cortana":
				await arg.Channel.SendMessageAsync($"Go ahead {arg.Author.Mention}");
				break;
			case "hi cortana":
				await arg.Channel.SendMessageAsync($"Hi {arg.Author.Mention}");
				break;
		}
	}

	private static bool MentionsCortana(SocketUserMessage message) =>
		message.MentionedUsers.Any(user => user.Id == DiscordUtils.Data.CortanaId);

	private static bool SessionOpen(ulong channel) =>
		Sessions.TryGetValue(channel, out DateTime expiry) && expiry > DateTime.UtcNow;

	private static async Task ExtendSession(ulong channel) =>
		Sessions[channel] = DateTime.UtcNow + await ChatWindow();

	private static async Task ReplyWithLlm(SocketMessage arg, SocketUserMessage message, bool mentioned)
	{
		string prompt = message.Content;
		foreach (SocketUser tagged in message.MentionedUsers)
			prompt = prompt.Replace($"<@{tagged.Id}>", "").Replace($"<@!{tagged.Id}>", "");
		prompt = prompt.Trim();

		if (prompt.Length == 0) return;

		var conversation = $"discord:{arg.Channel.Id}";
		if (mentioned && !SessionOpen(arg.Channel.Id)) await ApiHandler.Delete($"{ERoute.AI}/{conversation}");
		await ExtendSession(arg.Channel.Id);

		using IDisposable typing = arg.Channel.EnterTypingState();

		string author = (arg.Author as SocketGuildUser)?.DisplayName ?? arg.Author.Username;
		bool owner = arg.Author.Id == DiscordUtils.Data.ChiefId;
		string reply = await ApiHandler.Post($"{ERoute.AI}", new PostChat(prompt, conversation, author, Owner: owner));

		await ExtendSession(arg.Channel.Id);
		await arg.Channel.SendMessageAsync(reply, messageReference: new MessageReference(arg.Id));
	}

	private static async Task ActivityTimerElapsed(object? sender)
	{
		try
		{
			string messageResponse = await ApiHandler.Get($"{ERoute.Raspberry}/{ERaspberryInfo.Temperature}");
			await DiscordUtils.Cortana.SetActivityAsync(new Game($"on Raspberry at {messageResponse}"));
		}
		catch (Exception ex)
		{
			DataHandler.Log($"Could not update the activity status: {ex.Message}");
		}
	}

	private static Task OnUserVoiceStateUpdate(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
	{
		_ = Task.Run(() => HandleVoiceStateUpdate(user, oldState, newState));
		return Task.CompletedTask;
	}

	private static async Task HandleVoiceStateUpdate(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
	{
		if (oldState.VoiceChannel == newState.VoiceChannel) return;
		if (user.Id == DiscordUtils.Data.CortanaId) return;

		SocketGuild guild = (oldState.VoiceChannel ?? newState.VoiceChannel).Guild;
		if (!DiscordUtils.TryGetGuildSettings(guild.Id, out GuildSettings? settings)) return;

		_ = Task.Run(() => VoiceService.HandleConnection(guild));

		bool joined = oldState.VoiceChannel == null && newState.VoiceChannel != null;
		bool left = oldState.VoiceChannel != null && newState.VoiceChannel == null;
		if (!joined && !left) return;

		string? displayName = guild.GetUser(user.Id)?.DisplayName ?? user.Username;
		var footer = new EmbedFooterBuilder { IconUrl = user.GetAvatarUrl(), Text = joined ? "Joined at:" : "Left at:" };
		Embed embed = DiscordUtils.CreateEmbed(joined ? $"Hi {displayName}" : $"See you, {displayName}", withoutAuthor: true, footer: footer);

		if (settings.Greetings)
		{
			SocketTextChannel? greetingsChannel = guild.GetTextChannel(settings.GreetingsChannel);
			if (greetingsChannel != null) await greetingsChannel.SendMessageAsync(embed: embed);
		}

		if (left)
		{
			DiscordUtils.TimeConnected.TryRemove(user.Id, out _);
			return;
		}

		DiscordUtils.TimeConnected[user.Id] = DateTime.UtcNow;

		if (newState.VoiceChannel != null && VoiceService.IsConnectedTo(newState.VoiceChannel)) VoiceService.SayHello(guild.Id);
	}

	private static async Task OnServerJoin(SocketGuild guild)
	{
		DiscordUtils.AddGuildSettings(guild);
		await guild.DefaultChannel.SendMessageAsync(embed: DiscordUtils.CreateEmbed("Hi, I'm Cortana"));
	}

	private static async Task OnServerLeave(SocketGuild guild)
	{
		await VoiceService.RemoveGuild(guild.Id);
		DiscordUtils.GuildSettings.Remove(guild.Id);
		DiscordUtils.UpdateSettings();
	}

	private static Task OnUserJoin(SocketGuildUser user)
	{
		_ = Task.Run(() => HandleUserJoin(user));
		return Task.CompletedTask;
	}

	private static async Task HandleUserJoin(SocketGuildUser user)
	{
		if (user.IsBot) return;
		if (!DiscordUtils.TryGetGuildSettings(user.Guild.Id, out GuildSettings? settings)) return;

		SocketTextChannel? channel = user.Guild.GetTextChannel(settings.GreetingsChannel);
		if (channel != null) await channel.SendMessageAsync(embed: DiscordUtils.CreateEmbed($"Benvenuto {user.DisplayName}"));
	}

	private static Task LogAsync(LogMessage message)
	{
		string detail = message.Exception == null ? "" : $" | {message.Exception.GetType().Name}: {message.Exception.Message}";
		DataHandler.Log($"[{message.Severity}][{message.Source}] {message.Message}{detail}");
		return Task.CompletedTask;
	}

	private static DiscordSocketConfig ConfigureSocket()
	{
		return new DiscordSocketConfig
		{
			LogLevel = LogSeverity.Info,
			GatewayIntents = GatewayIntents.All,
			AlwaysDownloadUsers = true,
			UseInteractionSnowflakeDate = false,
			EnableVoiceDaveEncryption = DaveEnabled
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
