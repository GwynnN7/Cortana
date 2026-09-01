using System.Collections.Concurrent;
using System.Globalization;
using CortanaDiscord.Runtime;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;

CortanaEnvironment.Load(required: false);

var client = new DiscordSocketClient(new DiscordSocketConfig
{
	LogLevel = LogSeverity.Info,
	GatewayIntents = GatewayIntents.All,
	AlwaysDownloadUsers = true,
	UseInteractionSnowflakeDate = false
});

ServiceProvider services = new ServiceCollection()
	.AddSingleton(client)
	.AddSingleton(provider => new InteractionService(provider.GetRequiredService<DiscordSocketClient>()))
	.AddSingleton<CommandHandler>()
	.BuildServiceProvider();

DiscordContext.Use(client);

var commands = services.GetRequiredService<InteractionService>();
await services.GetRequiredService<CommandHandler>().Initialise();

ConcurrentDictionary<ulong, DateTimeOffset> sessions = new();
var started = 0;
TimeSpan chatWindow = TimeSpan.FromMinutes(1);
DateTimeOffset windowCheckedAt = DateTimeOffset.MinValue;

using var lifetime = new CancellationTokenSource();

client.Log += message =>
{
	Log.Write("Discord", message.Exception == null ? message.Message : $"{message.Message} | {message.Exception.Message}");
	return Task.CompletedTask;
};
commands.Log += message =>
{
	Log.Write("Discord", message.Message);
	return Task.CompletedTask;
};

client.Ready += OnReady;
client.Connected += OnReady;
client.MessageReceived += OnMessage;
client.UserVoiceStateUpdated += OnVoiceStateChanged;
client.UserJoined += OnUserJoined;
client.JoinedGuild += guild => guild.DefaultChannel.SendMessageAsync(embed: DiscordContext.Card("Hi, I'm Cortana"));
client.LeftGuild += guild =>
{
	DiscordContext.Forget(guild.Id);
	return Task.CompletedTask;
};

await client.LoginAsync(TokenType.Bot, CortanaEnvironment.Require("CORTANA_DISCORD_TOKEN"));
await client.StartAsync();

_ = Task.Run(() => FollowNotifications(lifetime.Token), lifetime.Token);

await ProcessSignals.WaitForShutdown();

await lifetime.CancelAsync();
await client.StopAsync();
await services.DisposeAsync();
Log.Write("Discord", "Offline");
return;

Task OnReady()
{
	if (Interlocked.Exchange(ref started, 1) == 1) return Task.CompletedTask;

	_ = Task.Run(async () =>
	{
		try
		{
			await commands.RegisterCommandsGloballyAsync();
			_ = Task.Run(() => ShowActivity(lifetime.Token), lifetime.Token);

			await DiscordContext.Post("I'm online", CortanaChannel.Cortana);
			Log.Write("Discord", "Online");
		}
		catch (Exception ex)
		{
			Log.Error("Discord", $"Startup failed: {ex.Message}");
			Interlocked.Exchange(ref started, 0);
		}
	});

	return Task.CompletedTask;
}

async Task OnMessage(SocketMessage received)
{
	if (received.Author.Id == DiscordContext.Ids.CortanaId) return;
	if (received is not SocketUserMessage message) return;

	if (received.Channel is SocketGuildChannel channel &&
		DiscordContext.SettingsFor(channel.Guild.Id) is { } settings &&
		settings.BannedWords.Any(word => received.Content.Contains(word, StringComparison.OrdinalIgnoreCase)))
	{
		await received.Channel.SendMessageAsync("That word is not allowed here, deleting the message");
		await received.DeleteAsync();
		return;
	}

	bool mentioned = message.MentionedUsers.Any(user => user.Id == DiscordContext.Ids.CortanaId);
	bool open = sessions.TryGetValue(received.Channel.Id, out DateTimeOffset expiry) && expiry > DateTimeOffset.UtcNow;

	if (mentioned || open)
	{
		await AnswerWithLlm(received, message, mentioned);
		return;
	}

	switch (received.Content.ToLowerInvariant())
	{
		case "cortana":
			await received.Channel.SendMessageAsync($"Go ahead {received.Author.Mention}");
			break;

		case "hi cortana":
			await received.Channel.SendMessageAsync($"Hi {received.Author.Mention}");
			break;
	}
}

// A mention opens a short conversation window
async Task AnswerWithLlm(SocketMessage received, SocketUserMessage message, bool mentioned)
{
	string prompt = message.MentionedUsers.Aggregate(message.Content,
		(text, user) => text.Replace($"<@{user.Id}>", "").Replace($"<@!{user.Id}>", "")).Trim();

	if (prompt.Length == 0) return;

	var conversation = $"discord:{received.Channel.Id}";
	if (mentioned && !sessions.ContainsKey(received.Channel.Id)) await DiscordContext.Cortana.ResetConversation(conversation);

	sessions[received.Channel.Id] = DateTimeOffset.UtcNow + await ChatWindow();

	using IDisposable typing = received.Channel.EnterTypingState();

	string author = (received.Author as SocketGuildUser)?.DisplayName ?? received.Author.Username;
	bool trusted = received.Author.Id == DiscordContext.Ids.ChiefId;

	string reply = await DiscordContext.Text(DiscordContext.Cortana.Ask(prompt, conversation, author, remember: true, trusted: trusted));

	sessions[received.Channel.Id] = DateTimeOffset.UtcNow + await ChatWindow();
	await received.Channel.SendMessageAsync(reply, messageReference: new MessageReference(received.Id));
}

async Task<TimeSpan> ChatWindow()
{
	if (DateTimeOffset.Now - windowCheckedAt < TimeSpan.FromSeconds(60)) return chatWindow;

	windowCheckedAt = DateTimeOffset.Now;
	Result<string> value = await DiscordContext.Cortana.AiSetting(AiSettingKey.DiscordSessionMinutes);

	if (value.IsOk && double.TryParse(value.Value.Trim(), CultureInfo.InvariantCulture, out double minutes) && minutes > 0)
		chatWindow = TimeSpan.FromMinutes(minutes);

	return chatWindow;
}

Task OnVoiceStateChanged(SocketUser user, SocketVoiceState before, SocketVoiceState after)
{
	_ = Task.Run(async () =>
	{
		if (before.VoiceChannel == after.VoiceChannel || user.Id == DiscordContext.Ids.CortanaId) return;

		SocketGuild guild = (before.VoiceChannel ?? after.VoiceChannel).Guild;
		if (DiscordContext.SettingsFor(guild.Id) is not { } settings) return;

		bool joined = before.VoiceChannel == null && after.VoiceChannel != null;
		bool left = before.VoiceChannel != null && after.VoiceChannel == null;
		if (!joined && !left) return;

		if (left)
		{
			DiscordContext.VoiceSince.TryRemove(user.Id, out _);
		}
		else
		{
			DiscordContext.VoiceSince[user.Id] = DateTimeOffset.UtcNow;
		}

		if (!settings.Greetings) return;

		string name = guild.GetUser(user.Id)?.DisplayName ?? user.Username;
		var footer = new EmbedFooterBuilder { IconUrl = user.GetAvatarUrl(), Text = joined ? "Joined at:" : "Left at:" };
		Embed card = DiscordContext.Card(joined ? $"Hi {name}" : $"See you, {name}", anonymous: true, footer: footer);

		SocketTextChannel? channel = guild.GetTextChannel(settings.GreetingsChannel);
		if (channel != null) await channel.SendMessageAsync(embed: card);
	});

	return Task.CompletedTask;
}

Task OnUserJoined(SocketGuildUser user)
{
	_ = Task.Run(async () =>
	{
		if (user.IsBot || DiscordContext.SettingsFor(user.Guild.Id) is not { } settings) return;

		SocketTextChannel? channel = user.Guild.GetTextChannel(settings.GreetingsChannel);
		if (channel != null) await channel.SendMessageAsync(embed: DiscordContext.Card($"Welcome {user.DisplayName}"));
	});

	return Task.CompletedTask;
}

async Task ShowActivity(CancellationToken token)
{
	using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

	do
	{
		try
		{
			Result<string> temperature = await DiscordContext.Cortana.RaspberryInfo(RaspberryInfo.Temperature);
			await client.SetActivityAsync(new Game(temperature.IsOk
				? $"on a Raspberry at {temperature.Value}"
				: "waiting for the Kernel"));
		}
		catch (Exception ex)
		{
			Log.Write("Discord", $"Could not set the activity: {ex.Message}");
		}
	}
	while (await timer.WaitForNextTickAsync(token));
}

async Task FollowNotifications(CancellationToken token)
{
	while (!token.IsCancellationRequested)
	{
		try
		{
			await foreach (NotificationEnvelope envelope in DiscordContext.Cortana.NotificationStream(NotificationChannel.Discord, token))
				await DiscordContext.Post(envelope.Notification.Message, CortanaChannel.Log);
		}
		catch (OperationCanceledException)
		{
			return;
		}
		catch (Exception ex)
		{
			Log.Write("Discord", $"The notification stream dropped: {ex.Message}");
			await Task.Delay(TimeSpan.FromSeconds(5), token);
		}
	}
}
