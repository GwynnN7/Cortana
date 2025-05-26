using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord.WebSocket;

namespace CortanaDiscord.Handlers;

internal static class AudioHandler
{
	public static readonly Dictionary<ulong, DiscordMediaHandler> MediaQueue = new();
	public static bool SayHello(ulong guildId)
	{
		string url = DataHandler.CortanaPath(EDirType.Storage, "hello.mp3");
		var track = new AudioTrack
		{
			Title = "Hello",
			OriginalUrl = url,
			StreamUrl = url,
			Duration = TimeSpan.Zero,
			ThumbnailUrl = ""
		};
		return Play(track, guildId);
	}

	public static bool Play(AudioTrack track, ulong guildId)
	{
		if (!MediaQueue.TryGetValue(guildId, out DiscordMediaHandler? handler) || handler.MediaPlayer == null) return false;

		handler.MediaPlayer.Enqueue(track);
		return true;
	}

	public static async Task<string> Skip(ulong guildId)
	{
		if (!MediaQueue.TryGetValue(guildId, out DiscordMediaHandler? handler) || handler.MediaPlayer == null) return "Non sono connessa al canale";
		var result = await handler.MediaPlayer.Skip();
		return result ? "Audio skippato" : "Non c'è niente da skippare";
	}
	
	public static string Clear(ulong guildId)
	{
		if (!MediaQueue.TryGetValue(guildId, out DiscordMediaHandler? handler) || handler.MediaPlayer == null) return "Non sono connessa al canale";
		var result = handler.MediaPlayer.Clear();
		return result ? "Queue rimossa" : "Non c'è niente in coda";
	}
	
	public static async Task<string> Stop(ulong guildId)
	{
		if (!MediaQueue.TryGetValue(guildId, out DiscordMediaHandler? handler) || handler.MediaPlayer == null) return "Non sono connessa al canale";
		var clearResult = Clear(guildId);
		var skipResult = await Skip(guildId);
		return $"{skipResult} ~ {clearResult}";
	}

	public static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild guild)
	{
		return guild.VoiceChannels.FirstOrDefault(voiceChannel => voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordUtils.Data.CortanaId));
	}

	public static bool IsConnected(SocketVoiceChannel voiceChannel, SocketGuild guild)
	{
		return MediaQueue.TryGetValue(guild.Id, out DiscordMediaHandler? player) && player.CurrentChannel == voiceChannel && GetCurrentCortanaChannel(guild) == voiceChannel;
	}

	public static void HandleConnection(SocketGuild guild)
	{
		if (ShouldTryReconnect(guild))
		{
			ReconnectToChannel(GetCurrentCortanaChannel(guild));
			return;
		}
		
		if (DiscordUtils.GuildSettings[guild.Id].AutoJoin)
		{
			SocketVoiceChannel? channel = GetAvailableChannel(guild);
			if (channel == null) Disconnect(guild.Id);
			else Connect(channel);
		}
		else Disconnect(guild.Id);
	}

	public static string Connect(SocketVoiceChannel channel)
	{
		if (GetCurrentCortanaChannel(channel.Guild) == channel) return "Sono già qui";

		if (!MediaQueue.TryGetValue(channel.Guild.Id, out DiscordMediaHandler? joiner))
		{
			joiner = new DiscordMediaHandler(channel.Guild);
			MediaQueue.Add(channel.Guild.Id, joiner);
		}
		joiner.Enqueue(new JoinAction(JoinStatus.Join, channel));

		return "Arrivo";
	}

	public static string Disconnect(ulong guildId)
	{
		try
		{
			if (!MediaQueue.TryGetValue(guildId, out DiscordMediaHandler? joiner))
				throw new CortanaException("Not connected to channel");
			
			joiner.Enqueue(new JoinAction(JoinStatus.Leave, null));

			return "Mi sto disconnettendo";
		}
		catch
		{
			return "Non sono connessa a nessun canale";
		}
	}

	private static void ReconnectToChannel(SocketVoiceChannel? channel)
	{
		if (channel == null) return;
		if (IsConnected(channel, channel.Guild)) return;
		Connect(channel);
	}
	
	public static SocketVoiceChannel? GetAvailableChannel(SocketGuild guild)
	{
		return guild.VoiceChannels.FirstOrDefault(IsChannelAvailable);
	}

	public static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild guild)
	{
		List<SocketVoiceChannel> channels = [];
		channels.AddRange(guild.VoiceChannels.Where(IsChannelAvailable));
		return channels;
	}
	
	private static bool IsChannelAvailable(SocketVoiceChannel channel)
	{
		if (channel.Id == DiscordUtils.GuildSettings[channel.Guild.Id].AfkChannel) return false;
		if (channel.ConnectedUsers.Select(user => user.Id).Contains(DiscordUtils.Data.CortanaId)) return channel.ConnectedUsers.Count > 1;
		return channel.ConnectedUsers.Count > 0;
	}
	
	private static bool ShouldTryReconnect(SocketGuild guild)
	{
		SocketVoiceChannel? currentChannel = GetCurrentCortanaChannel(guild);
		List<SocketVoiceChannel> availableChannels = GetAvailableChannels(guild);

		if (availableChannels.Count == 0) return currentChannel == null;
		return currentChannel != null && availableChannels.Contains(currentChannel);
	}
}