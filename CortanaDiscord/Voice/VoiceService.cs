using System.Collections.Concurrent;
using CortanaDiscord.Utility;
using CortanaLib;
using Discord.WebSocket;

namespace CortanaDiscord.Voice;

internal static class VoiceService
{
	private const string NotConnected = "Non sono connessa a nessun canale";

	private static readonly ConcurrentDictionary<ulong, VoiceSession> Sessions = new();

	public static IReadOnlyCollection<ulong> ActiveGuilds => Sessions.Keys.ToList();

	private static VoiceSession GetOrCreate(SocketGuild guild) =>
		Sessions.GetOrAdd(guild.Id, _ => new VoiceSession(guild));

	private static VoiceSession? Get(ulong guildId) => Sessions.GetValueOrDefault(guildId);

	public static Task<string> Connect(SocketVoiceChannel channel) => GetOrCreate(channel.Guild).ConnectAsync(channel);

	public static async Task<string> Disconnect(ulong guildId)
	{
		VoiceSession? session = Get(guildId);
		return session == null ? NotConnected : await session.DisconnectAsync();
	}

	public static async Task DisconnectAll()
	{
		await Task.WhenAll(Sessions.Values.Select(session => session.DisposeAsync().AsTask()));
		Sessions.Clear();
	}

	public static async Task RemoveGuild(ulong guildId)
	{
		if (Sessions.TryRemove(guildId, out VoiceSession? session)) await session.DisposeAsync();
	}

	public static async Task HandleConnection(SocketGuild guild)
	{
		if (!DiscordUtils.TryGetGuildSettings(guild.Id, out GuildSettings? settings)) return;

		SocketVoiceChannel? currentChannel = GetCurrentCortanaChannel(guild);

		if (currentChannel != null)
		{
			if (!IsChannelAvailable(guild, currentChannel)) await Disconnect(guild.Id);
			return;
		}

		if (!settings.AutoJoin)
		{
			await Disconnect(guild.Id);
			return;
		}

		SocketVoiceChannel? target = GetAvailableChannel(guild);
		if (target == null) await Disconnect(guild.Id);
		else await Connect(target);
	}

	public static bool Play(AudioTrack track, ulong guildId) => Get(guildId)?.Enqueue(track) ?? false;

	public static bool SayHello(ulong guildId) => Play(VoiceSession.HelloTrack(), guildId);

	public static async Task<string> Skip(ulong guildId)
	{
		VoiceSession? session = Get(guildId);
		if (session == null) return NotConnected;
		return await session.SkipAsync() ? "Audio skippato" : "Non c'è niente da skippare";
	}

	public static string Clear(ulong guildId)
	{
		VoiceSession? session = Get(guildId);
		if (session == null) return NotConnected;
		return session.Clear() ? "Queue rimossa" : "Non c'è niente in coda";
	}

	public static async Task<string> Stop(ulong guildId)
	{
		VoiceSession? session = Get(guildId);
		if (session == null) return NotConnected;

		string clearResult = session.Clear() ? "Queue rimossa" : "Non c'è niente in coda";
		string skipResult = await session.SkipAsync() ? "Audio skippato" : "Non c'è niente da skippare";
		return $"{skipResult} ~ {clearResult}";
	}

	public static IReadOnlyCollection<string> GetQueue(ulong guildId) => Get(guildId)?.QueuedTitles ?? [];

	public static AudioTrack? NowPlaying(ulong guildId) => Get(guildId)?.CurrentTrack;

	public static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild guild) =>
		guild.VoiceChannels.FirstOrDefault(channel => channel.ConnectedUsers.Any(user => user.Id == DiscordUtils.Data.CortanaId));

	public static bool IsConnectedTo(SocketVoiceChannel channel) =>
		Get(channel.Guild.Id) is { IsConnected: true } session && session.CurrentChannel?.Id == channel.Id;

	public static SocketVoiceChannel? GetAvailableChannel(SocketGuild guild) =>
		guild.VoiceChannels.FirstOrDefault(channel => IsChannelAvailable(guild, channel));

	public static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild guild) =>
		guild.VoiceChannels.Where(channel => IsChannelAvailable(guild, channel)).ToList();

	private static bool IsChannelAvailable(SocketGuild guild, SocketVoiceChannel channel)
	{
		if (!DiscordUtils.TryGetGuildSettings(guild.Id, out GuildSettings? settings)) return false;
		if (channel.Id == settings.AfkChannel) return false;

		int humans = channel.ConnectedUsers.Count(user => user.Id != DiscordUtils.Data.CortanaId);
		return humans > 0;
	}
}
