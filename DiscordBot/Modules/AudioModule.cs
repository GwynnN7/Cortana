using CliWrap;
using Discord.Audio;
using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Processor;
using YoutubeExplode.Videos;

namespace DiscordBot.Modules
{
    public static class AudioHandler
    {
        public struct ChannelClient
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

        private readonly struct QueueStructure(CancellationTokenSource newToken, MemoryStream newStream, ulong newGuildId)
        {
            public CancellationTokenSource Token { get; } = newToken;
            public MemoryStream Data { get; } = newStream;
            public ulong GuildId { get; } = newGuildId;
        }

        private readonly struct JoinStructure(CancellationTokenSource newToken, Func<Task> newTask)
        {
            public CancellationTokenSource Token { get; } = newToken;
            public Func<Task> Task { get; } = newTask;
        }
        
        public static readonly Dictionary<ulong, ChannelClient> AudioClients = new();
        private static readonly Dictionary<ulong, Queue<QueueStructure>> AudioQueue = new();
        private static readonly Dictionary<ulong, Queue<JoinStructure>> JoinQueue = new();
        
        //---------------------------- Audio Functions ----------------------------------------------

        private static async Task<MemoryStream> ExecuteFfmpeg(Stream? videoStream = null, string filePath = "")
        {
            var memoryStream = new MemoryStream();
            await Cli.Wrap("ffmpeg")
                .WithArguments($" -hide_banner -loglevel debug -i {(videoStream != null ? "pipe:0" : $"\"{filePath}\"")} -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe((videoStream != null ? PipeSource.FromStream(videoStream) : PipeSource.Null))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync();
            return memoryStream;
        }

        public static async Task<bool> PlayMusic(string audio, ulong guildId)
        {
            Stream stream = await Software.GetAudioStream(audio);
            MemoryStream memoryStream = await ExecuteFfmpeg(videoStream: stream);
            
            return Play(memoryStream, guildId);
        }
        
        public static async Task<bool> SayHello(ulong guildId)
        {
            MemoryStream memoryStream = await ExecuteFfmpeg(filePath: "Storage/Sound/Hello.mp3");
            
            return Play(memoryStream, guildId);
        }

        private static bool Play(MemoryStream memoryStream, ulong guildId)
        {
            if (!AudioClients.TryGetValue(guildId, out ChannelClient client) || client.AudioStream == null) return false;
            
            var audioQueueItem = new QueueStructure(new CancellationTokenSource(), memoryStream, guildId);
            if (AudioQueue.TryGetValue(guildId, out Queue<QueueStructure>? queueStructures)) queueStructures.Enqueue(audioQueueItem);
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
            try {
                await AudioClients[item.GuildId].AudioStream!.WriteAsync(item.Data.GetBuffer(), item.Token.Token);
            }
            finally {
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
                else Disconnect(guild.Id);
            }
            else EnsureChannel(GetCurrentCortanaChannel(guild));
        }

        private static void AddToJoinQueue(Func<Task> taskToAdd, ulong guildId)
        {
            var queueItem = new JoinStructure(new CancellationTokenSource(), taskToAdd);
            if (JoinQueue.TryGetValue(guildId, out Queue<JoinStructure>? joinStructures)) joinStructures.Enqueue(queueItem);
            else
            {
                var queue = new Queue<JoinStructure>();
                queue.Enqueue(queueItem);
                JoinQueue.Add(guildId, queue);
            }

            if (JoinQueue[guildId].Count == 1) NextJoinQueue(guildId);
        }

        private static async void NextJoinQueue(ulong guildId)
        {
            JoinStructure joinItem = JoinQueue[guildId].Dequeue();
            await joinItem.Task();
            joinItem.Token.Dispose();
            if (JoinQueue[guildId].Count <= 0) return;
            while(JoinQueue[guildId].Count != 1) JoinQueue[guildId].Dequeue();

            NextJoinQueue(guildId);
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
                DiscordUtils.SendToChannel("Non sono riuscita ad entrate nel canale correttamente", ECortanaChannels.Log);
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
                DiscordUtils.SendToChannel("Non sono riuscita ad uscire dal canale correttamente", ECortanaChannels.Log);
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

    [Group("media", "Gestione audio")]
    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("play", "Metti qualcosa da youtube", runMode: RunMode.Async)]
        public async Task Play([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            Video result = await Software.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration ?? TimeSpan.Zero;
            Embed embed = DiscordUtils.CreateEmbed(result.Title, description: $@"{duration:hh\:mm\:ss}");
            embed = embed.ToEmbedBuilder()
                .WithUrl(result.Url)
                .WithThumbnailUrl(result.Thumbnails[^1].Url)
                .Build();

            await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

            bool status = await AudioHandler.PlayMusic(result.Url, Context.Guild.Id);
            if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il video");
        }

        [SlashCommand("skip", "Skippa quello che sto dicendo")]
        public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Skip(Context.Guild.Id);
            await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("stop", "Rimuovi tutto quello che c'è in coda")]
        public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Clear(Context.Guild.Id);
            await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("join", "Entro nel canale dove sono stata chiamata")]
        public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            var text = "Non posso connettermi se non sei in un canale";
            foreach(SocketVoiceChannel? voiceChannel in Context.Guild.VoiceChannels)
            {
                if (!voiceChannel.ConnectedUsers.Contains(Context.User)) continue;
                text = AudioHandler.Connect(voiceChannel);
                break;
            }
            await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("leave", "Esco dal canale vocale")]
        public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string text = AudioHandler.Disconnect(Context.Guild.Id);
            await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("scarica-musica", "Scarica una canzone da youtube", runMode: RunMode.Async)]
        public async Task DownloadMusic([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            Video result = await Software.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration ?? TimeSpan.Zero;
            Embed embed = DiscordUtils.CreateEmbed(result.Title, description: $@"{duration:hh\:mm\:ss}");
            embed = embed.ToEmbedBuilder()
                .WithDescription("Musica in download...")
                .WithUrl(result.Url)
                .WithThumbnailUrl(result.Thumbnails[^1].Url)
                .Build();

            await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

            Stream stream = await Software.GetAudioStream(result.Url);
            await Context.Channel.SendFileAsync(stream, result.Title + ".mp3");
        }
        
        [SlashCommand("meme", "Metto un meme tra quelli disponibili")]
        public async Task Meme([Summary("nome", "Nome del meme")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            foreach((string title, MemeJsonStructure memeStruct) in DiscordUtils.Memes)
            {
                if (!memeStruct.Alias.Contains(name.ToLower())) continue;
                string link = memeStruct.Link;

                Video result = await Software.GetYoutubeVideoInfos(link);
                TimeSpan duration = result.Duration ?? TimeSpan.Zero;
                Embed embed = DiscordUtils.CreateEmbed(title, description: $@"{duration:hh\:mm\:ss}");
                embed = embed.ToEmbedBuilder()
                    .WithUrl(result.Url)
                    .WithThumbnailUrl(result.Thumbnails[^1].Url)
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

                bool status = await AudioHandler.PlayMusic(result.Url, Context.Guild.Id);
                if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mettere il meme");
                return;
            }
            await FollowupAsync("Non ho nessun meme salvato con quel nome", ephemeral: ephemeral == EAnswer.Si);
        }
        
        [SlashCommand("elenco-meme", "Lista dei meme disponibili")]
        public async Task GetMemes([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
        {
            Embed embed = DiscordUtils.CreateEmbed("Memes");
            var tempEmbed = embed.ToEmbedBuilder();
            foreach (EMemeCategory category in Enum.GetValues(typeof(EMemeCategory)))
            {
                string categoryString = DiscordUtils.Memes.Where(meme => meme.Value.Category == category).Aggregate("", (current, meme) => current + $"[{meme.Key}]({meme.Value.Link})\n");
                if (categoryString.Length == 0) continue;
                tempEmbed.AddField(category.ToString(), categoryString);
            }
            await RespondAsync(embed: tempEmbed.Build(), ephemeral: ephemeral == EAnswer.Si);
        }
    }
}
