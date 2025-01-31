using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord.Audio;
using Discord.WebSocket;

namespace CortanaDiscord.Handlers;

internal static class AudioHandler
{
	public static readonly Dictionary<ulong, ChannelClient> AudioClients = new();
	private static readonly Dictionary<ulong, Queue<QueueStructure>> AudioQueue = new();
	private static readonly Dictionary<ulong, Queue<JoinStructure>> JoinQueue = new();

	//---------------------------- Audio Functions ----------------------------------------------

	public static async Task<bool> PlayMusic(string audio, ulong guildId)
	{
		Stream stream = await MediaHandler.GetAudioStream(audio);
		MemoryStream memoryStream = await MediaHandler.ExecuteFfmpeg(stream);

		return Play(memoryStream, guildId);
	}

	public static async Task<bool> SayHello(ulong guildId)
	{
		MemoryStream memoryStream = await MediaHandler.ExecuteFfmpeg(filePath: FileHandler.GetPath(EDirType.Storage, "Hello.mp3"));

		return Play(memoryStream, guildId);
	}

	private static bool Play(MemoryStream memoryStream, ulong guildId)
	{
		if (!AudioClients.TryGetValue(guildId, out ChannelClient client) || client.AudioStream == null) return false;

		var audioQueueItem = new QueueStructure(new CancellationTokenSource(), memoryStream, guildId);
		if (AudioQueue.TryGetValue(guildId, out Queue<QueueStructure>? queueStructures))
		{
			queueStructures.Enqueue(audioQueueItem);
		}
		else
		{
			var queue = new Queue<QueueStructure>();
			queue.Enqueue(audioQueueItem);
			AudioQueue.Add(guildId, queue);
		}

		if (AudioQueue[guildId].Count == 1) NextAudioQueue(guildId);
		return true;
	}

	private static void NextAudioQueue(ulong guildId)
	{
		Task audioTask = Task.Run(() => SendBuffer(AudioQueue[guildId].Peek()));
		audioTask.ContinueWith(_ =>
		{
			if (!AudioQueue[guildId].TryDequeue(out QueueStructure queueItem)) return;
			queueItem.Token.Dispose();
			queueItem.Data.Dispose();
			if (AudioQueue[guildId].Count > 0) NextAudioQueue(guildId);
		});
	}

	private static async Task SendBuffer(QueueStructure item)
	{
		try
		{
			await AudioClients[item.GuildId].AudioStream!.WriteAsync(item.Data.GetBuffer(), item.Token.Token);
		}
		finally
		{
			await AudioClients[item.GuildId].AudioStream!.FlushAsync();
		}
	}

	public static string Skip(ulong guildId)
	{
		if (!AudioQueue.TryGetValue(guildId, out Queue<QueueStructure>? audioQueue)) return "Non c'è niente da skippare";
		if (audioQueue.Count <= 0) return "Non c'è niente da skippare";
		audioQueue.Peek().Token.Cancel();
		return "Audio skippato";
	}

	public static string Clear(ulong guildId)
	{
		if (!AudioQueue.TryGetValue(guildId, out Queue<QueueStructure>? queue) || queue.Count <= 0) return "Non c'è niente in coda";
		var copyQueue = new Queue<QueueStructure>(queue);
		queue.Clear();
		while (copyQueue.Count > 0)
		{
			QueueStructure queueItem = copyQueue.Dequeue();
			queueItem.Token.Cancel();
			queueItem.Token.Dispose();
			queueItem.Data.Dispose();
		}

		return "Queue rimossa";
	}

	//-------------------------------------------------------------------------------------------

	//------------------------ Channels Checking Function ---------------------------------------

	private static bool ShouldCortanaStay(SocketGuild guild)
	{
		SocketVoiceChannel? cortanaChannel = GetCurrentCortanaChannel(guild);
		List<SocketVoiceChannel> availableChannels = GetAvailableChannels(guild);

		if (availableChannels.Count <= 0) return cortanaChannel == null;
		return cortanaChannel != null && availableChannels.Contains(cortanaChannel);
	}

	private static bool IsChannelAvailable(SocketVoiceChannel channel)
	{
		if (channel.Id == DiscordUtils.GuildSettings[channel.Guild.Id].AfkChannel) return false;
		if (channel.ConnectedUsers.Select(x => x.Id).Contains(DiscordUtils.Data.CortanaId)) return channel.ConnectedUsers.Count > 1;
		return channel.ConnectedUsers.Count > 0;
	}

	public static SocketVoiceChannel? GetAvailableChannel(SocketGuild guild)
	{
		return guild.VoiceChannels.FirstOrDefault(IsChannelAvailable);
	}

	private static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild guild)
	{
		List<SocketVoiceChannel> channels = [];
		channels.AddRange(guild.VoiceChannels.Where(IsChannelAvailable));
		return channels;
	}

	public static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild guild)
	{
		return guild.VoiceChannels.FirstOrDefault(voiceChannel => voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordUtils.Data.CortanaId));
	}

	private static bool IsConnected(SocketVoiceChannel voiceChannel, SocketGuild guild)
	{
		return AudioClients.TryGetValue(guild.Id, out ChannelClient client) && client.VoiceChannel == voiceChannel && GetCurrentCortanaChannel(guild) == voiceChannel;
	}

	//-------------------------------------------------------------------------------------------

	//-------------------------- Connection Functions -------------------------------------------

	public static void HandleConnection(SocketGuild guild)
	{
		if (!ShouldCortanaStay(guild))
		{
			if (DiscordUtils.GuildSettings[guild.Id].AutoJoin)
			{
				SocketVoiceChannel? channel = GetAvailableChannel(guild);
				if (channel == null) Disconnect(guild.Id);
				else Connect(channel);
			}
			else
			{
				Disconnect(guild.Id);
			}
		}
		else
		{
			EnsureChannel(GetCurrentCortanaChannel(guild));
		}
	}

	private static void AddToJoinQueue(Func<Task> taskToAdd, ulong guildId)
	{
		var queueItem = new JoinStructure(new CancellationTokenSource(), taskToAdd);
		if (JoinQueue.TryGetValue(guildId, out Queue<JoinStructure>? joinStructures))
		{
			joinStructures.Enqueue(queueItem);
		}
		else
		{
			var queue = new Queue<JoinStructure>();
			queue.Enqueue(queueItem);
			JoinQueue.Add(guildId, queue);
		}

		if (JoinQueue[guildId].Count == 1) Task.Run(async() => await NextJoinQueue(guildId));
	}

	private static async Task NextJoinQueue(ulong guildId)
	{
		JoinStructure joinItem = JoinQueue[guildId].Dequeue();
		await joinItem.Task();
		joinItem.Token.Dispose();
		if (JoinQueue[guildId].Count <= 0) return;
		while (JoinQueue[guildId].Count != 1) JoinQueue[guildId].Dequeue();

		await NextJoinQueue(guildId);
	}

	private static async Task Join(SocketVoiceChannel voiceChannel)
	{
		SocketGuild guild = voiceChannel.Guild;

		try
		{
			await Task.Delay(1500);

			if (!GetAvailableChannels(voiceChannel.Guild).Contains(voiceChannel)) return;
			if (IsConnected(voiceChannel, guild)) return;

			Clear(guild.Id);
			DisposeConnection(guild.Id);

			var newPair = new ChannelClient(voiceChannel);
			AudioClients.Add(guild.Id, newPair);

			IAudioClient? audioClient = await voiceChannel.ConnectAsync();
			AudioOutStream? streamOut = audioClient.CreatePCMStream(AudioApplication.Mixed, 64000, packetLoss: 0);
			AudioClients[guild.Id] = new ChannelClient(voiceChannel, audioClient, streamOut);

			await SayHello(guild.Id);
		}
		catch
		{
			await DiscordUtils.SendToChannel<string>("Non sono riuscita ad entrate nel canale correttamente", ECortanaChannels.Log);
		}
	}

	private static async Task Leave(SocketVoiceChannel voiceChannel)
	{
		SocketGuild guild = voiceChannel.Guild;

		try
		{
			Clear(guild.Id);
			DisposeConnection(guild.Id);
			if (voiceChannel == GetCurrentCortanaChannel(guild)) await voiceChannel.DisconnectAsync();
		}
		catch
		{
			await DiscordUtils.SendToChannel<string>("Non sono riuscita ad uscire dal canale correttamente", ECortanaChannels.Log);
		}
	}

	private static void DisposeConnection(ulong guildId)
	{
		if (!AudioClients.TryGetValue(guildId, out ChannelClient channel)) return;
		channel.AudioStream?.Dispose();
		channel.AudioClient?.Dispose();
		AudioClients.Remove(guildId);
	}

	public static string Connect(SocketVoiceChannel channel)
	{
		if (GetCurrentCortanaChannel(channel.Guild) == channel) return "Sono già qui";
		AddToJoinQueue(() => Join(channel), channel.Guild.Id);

		return "Arrivo";
	}

	public static string Disconnect(ulong guildId)
	{
		foreach ((ulong clientId, ChannelClient clientChannel) in AudioClients)
		{
			if (clientId != guildId) continue;
			AddToJoinQueue(() => Leave(clientChannel.VoiceChannel), guildId);

			return "Mi sto disconnettendo";
		}

		return "Non sono connessa a nessun canale";
	}

	private static void EnsureChannel(SocketVoiceChannel? channel)
	{
		if (channel == null) return;
		if (IsConnected(channel, channel.Guild)) return;
		AddToJoinQueue(() => Join(channel), channel.Guild.Id);
	}
	//-------------------------------------------------------------------------------------------
}

internal struct ChannelClient
{
	public SocketVoiceChannel VoiceChannel { get; }
	public IAudioClient? AudioClient { get; } = null;
	public AudioOutStream? AudioStream { get; } = null;

	public ChannelClient(SocketVoiceChannel newVoiceChannel, IAudioClient? newAudioClient = null, AudioOutStream? newAudioStream = null)
	{
		VoiceChannel = newVoiceChannel;
		AudioClient = newAudioClient;
		AudioStream = newAudioStream;
	}
}

internal readonly struct QueueStructure(CancellationTokenSource newToken, MemoryStream newStream, ulong newGuildId)
{
	public CancellationTokenSource Token { get; } = newToken;
	public MemoryStream Data { get; } = newStream;
	public ulong GuildId { get; } = newGuildId;
}

internal readonly struct JoinStructure(CancellationTokenSource newToken, Func<Task> newTask)
{
	public CancellationTokenSource Token { get; } = newToken;
	public Func<Task> Task { get; } = newTask;
}