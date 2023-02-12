using CliWrap;
using Discord.Audio;
using Discord.Interactions;
using Discord.WebSocket;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using Discord;

namespace DiscordBot.Modules
{
    public static class AudioHandler
    {
        public struct ChannelClient
        {
            public SocketVoiceChannel VoiceChannel { get; set; }
            public IAudioClient? AudioClient { get; set; } = null;
            public AudioOutStream? AudioStream { get; set; } = null;

            public ChannelClient(SocketVoiceChannel NewVoiceChannel, IAudioClient? NewAudioClient = null, AudioOutStream? NewAudioStream = null)
            {
                VoiceChannel = NewVoiceChannel;
                AudioClient = NewAudioClient;
                AudioStream = NewAudioStream;
            }
        }

        public struct QueueStructure
        {
            public CancellationTokenSource Token { get; }
            public MemoryStream Data { get; }
            public ulong GuildID { get; }

            public QueueStructure(CancellationTokenSource NewToken, MemoryStream NewStream, ulong NewGuildID)
            {
                Token = NewToken;
                Data = NewStream;
                GuildID = NewGuildID;
            }
        }

        public struct JoinStructure
        {
            public CancellationTokenSource Token { get; }
            public ulong GuildID { get; }
            public Func<Task> Task { get; set; }

            public JoinStructure(CancellationTokenSource NewToken, ulong NewGuildID, Func<Task> NewTask)
            {
                Token = NewToken;
                GuildID = NewGuildID;
                Task = NewTask;
            }
        }
        public static Dictionary<ulong, ChannelClient> AudioClients = new Dictionary<ulong, ChannelClient>();

        private static Dictionary<ulong, List<QueueStructure>> AudioQueue = new Dictionary<ulong, List<QueueStructure>>();
        private static Dictionary<ulong, List<JoinStructure>> JoinQueue = new Dictionary<ulong, List<JoinStructure>>();
        
        //---------------------------- Audio Functions ----------------------------------------------

        private static async Task<MemoryStream> ExecuteFFMPEG(Stream? VideoStream = null, string FilePath = "")
        {
            var memoryStream = new MemoryStream();
            await Cli.Wrap("ffmpeg")
                .WithArguments($" -hide_banner -loglevel debug -i {(VideoStream != null ? "pipe:0" : $"\"{FilePath}\"")} -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe((VideoStream != null ? PipeSource.FromStream(VideoStream) : PipeSource.Null))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync();
            return memoryStream;
        }

        public static async Task<Stream> GetYoutubeAudioStream(string url)
        {
            YoutubeClient youtube = new YoutubeClient();
            var StreamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var StreamInfo = StreamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            var Stream = await youtube.Videos.Streams.GetAsync(StreamInfo);
            return Stream;
        }

        public static async Task<Stream> GetYoutubeVideoStream(string url)
        {
            YoutubeClient youtube = new YoutubeClient();
            var StreamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var StreamInfo = StreamManifest.GetMuxedStreams();
            
            var NewList = StreamInfo.ToList();
            NewList.Sort(delegate (MuxedStreamInfo a, MuxedStreamInfo b)
            {
                if (a.Size.Bytes >= b.Size.Bytes) return 1;
                else return -1;
            });

            foreach (var video in NewList)
            {
                if (video.Size.Bytes < 8388608)
                {
                    var Stream = await youtube.Videos.Streams.GetAsync(video);
                    return Stream;
                }
            }
            return Stream.Null;
        }

        public static async Task<YoutubeExplode.Videos.Video> GetYoutubeVideoInfos(string url)
        {
            var youtube = new YoutubeClient();

            var link = url.Split("&").First();
            var substrings = new[] { "https://www.youtube.com/watch?v=", "https://youtu.be/" };
            string? result = null;
            foreach (var sub in substrings)
            {
                if (link.StartsWith(sub)) result = link.Substring(sub.Length);
            }
            if(result == null)
            {
                var videos = await youtube.Search.GetVideosAsync(url).CollectAsync(1);
                result = videos.First().Id;
            }
            return await youtube.Videos.GetAsync(result);
        }

        public static async Task<bool> Play(string audio, ulong GuildID, EAudioSource Path)
        {
            if (!AudioClients.ContainsKey(GuildID) || (AudioClients.ContainsKey(GuildID) && AudioClients[GuildID].AudioStream == null)) return false;

            MemoryStream MemoryStream = new MemoryStream();
            if (Path == EAudioSource.Youtube)
            {
                var Stream = await GetYoutubeAudioStream(audio);
                MemoryStream = await ExecuteFFMPEG(VideoStream: Stream);
            }
            else if (Path == EAudioSource.Local)
            {
                audio = $"Sound/{audio}.mp3";
                MemoryStream = await ExecuteFFMPEG(FilePath: audio);
            }

            var AudioQueueItem = new QueueStructure(new CancellationTokenSource(), MemoryStream, GuildID);
            if (AudioQueue.ContainsKey(GuildID)) AudioQueue[GuildID].Add(AudioQueueItem);
            else AudioQueue.Add(GuildID, new List<QueueStructure>() { AudioQueueItem });

            if (AudioQueue[GuildID].Count == 1) NextAudioQueue(GuildID);
            
            return true;
        }

        private static void NextAudioQueue(ulong GuildID)
        {
            var AudioTask = Task.Run(() => SendBuffer(AudioQueue[GuildID][0]));
            AudioTask.ContinueWith((NewTask) =>
            {
                AudioQueue[GuildID][0].Token.Dispose();
                AudioQueue[GuildID][0].Data.Dispose();
                AudioQueue[GuildID].RemoveAt(0);
                if (AudioQueue[GuildID].Count > 0) NextAudioQueue(GuildID);
            });
        }  

        private static async Task SendBuffer(QueueStructure Item)
        {
            try
            {
                await AudioClients[Item.GuildID].AudioStream.WriteAsync(Item.Data.GetBuffer(), Item.Token.Token);
            }
            finally
            {
                await AudioClients[Item.GuildID].AudioStream.FlushAsync();
            }
        }

        public static string Skip(ulong GuildID)
        {
            if (AudioQueue.ContainsKey(GuildID))
            {
                if (AudioQueue[GuildID].Count > 0)
                {
                    AudioQueue[GuildID][0].Token.Cancel();
                    return "Audio skippato";
                }
            }
            return "Non c'è niente da skippare";
        }

        public static string Clear(ulong GuildID)
        {
            if (AudioQueue.ContainsKey(GuildID) && AudioQueue[GuildID].Count > 0)
            {
                for(int i = AudioQueue[GuildID].Count - 1; i >= 0; i--)
                {
                    if(i == 0)
                    {
                        AudioQueue[GuildID][i].Token.Cancel();
                        continue;
                    }

                    AudioQueue[GuildID][i].Token.Cancel();
                    AudioQueue[GuildID][i].Token.Dispose();

                    AudioQueue[GuildID][i].Data.Dispose();

                    AudioQueue[GuildID].RemoveAt(i);
                }
            
                AudioQueue[GuildID].Clear();
                return "Queue rimossa";
            }
            return "Non c'è niente in coda";
        }

        //-------------------------------------------------------------------------------------------

        //------------------------ Channels Checking Function ---------------------------------------

        private static bool ShouldCortanaStay(SocketGuild Guild)
        {
            var CortanaChannel = GetCurrentCortanaChannel(Guild);
            var AvailableChannels = GetAvailableChannels(Guild);

            if (AvailableChannels.Count > 0)
            {
                if (CortanaChannel == null) return false;
                else return AvailableChannels.Contains(CortanaChannel);
            }
            else return CortanaChannel == null;
        }

        public static bool IsChannelAvailable(SocketVoiceChannel Channel)
        {
            if (Channel.Id == DiscordData.GuildSettings[Channel.Guild.Id].AFKChannel) return false;
            if (Channel.ConnectedUsers.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) return Channel.ConnectedUsers.Count > 1;
            else return Channel.ConnectedUsers.Count > 0;
        }

        public static SocketVoiceChannel? GetAvailableChannel(SocketGuild Guild)
        {
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if(IsChannelAvailable(voiceChannel)) return voiceChannel;
            }
            return null;
        }

        private static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild Guild)
        {
            List<SocketVoiceChannel> channels = new List<SocketVoiceChannel>();
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (IsChannelAvailable(voiceChannel)) channels.Add(voiceChannel);
            }
            return channels;
        }

        public static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild Guild)
        {
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) return voiceChannel;
            }
            return null;
        }

        private static bool IsConnected(SocketVoiceChannel VoiceChannel, SocketGuild Guild)
        {
            if (AudioClients.ContainsKey(Guild.Id) && AudioClients[Guild.Id].VoiceChannel == VoiceChannel && GetCurrentCortanaChannel(Guild) == VoiceChannel) return true;
            return false;
        }

        //-------------------------------------------------------------------------------------------

        //-------------------------- Connection Functions -------------------------------------------

        public static void HandleConnection(SocketGuild Guild)
        {
            if (!ShouldCortanaStay(Guild))
            {
                if (DiscordData.GuildSettings[Guild.Id].AutoJoin)
                {
                    var channel = GetAvailableChannel(Guild);
                    if (channel == null) Disconnect(Guild.Id);
                    else Connect(channel);
                }
                else Disconnect(Guild.Id);
            }
            else EnsureChannel(GetCurrentCortanaChannel(Guild));
        }

        private static void AddToJoinQueue(Func<Task> TaskToAdd, ulong GuildID)
        {
            var QueueItem = new JoinStructure(new CancellationTokenSource(), GuildID, TaskToAdd);
            if (JoinQueue.ContainsKey(GuildID)) JoinQueue[GuildID].Add(QueueItem);
            else JoinQueue.Add(GuildID, new List<JoinStructure>() { QueueItem });

            if (JoinQueue[GuildID].Count == 1) NextJoinQueue(GuildID);
        }

        private static async void NextJoinQueue(ulong GuildID)
        {
            await JoinQueue[GuildID][0].Task();

            JoinQueue[GuildID][0].Token.Dispose();
            JoinQueue[GuildID].RemoveAt(0);
            if (JoinQueue[GuildID].Count > 0)
            {
                JoinQueue[GuildID] = new List<JoinStructure>() { JoinQueue[GuildID].Last() };

                NextJoinQueue(GuildID);
            }
        }

        private static async Task Join(SocketVoiceChannel VoiceChannel)
        {
            if (VoiceChannel == null) return;
            SocketGuild Guild = VoiceChannel.Guild;

            try
            {
                await Task.Delay(1500);

                if (!GetAvailableChannels(VoiceChannel.Guild).Contains(VoiceChannel)) return;
                else if (IsConnected(VoiceChannel, Guild)) return;

                Clear(Guild.Id);
                DisposeConnection(Guild.Id);

                var NewPair = new ChannelClient(VoiceChannel);
                AudioClients.Add(Guild.Id, NewPair);

                var AudioClient = await VoiceChannel.ConnectAsync();
                var StreamOut = AudioClient.CreatePCMStream(AudioApplication.Mixed, 64000, packetLoss: 0);
                AudioClients[Guild.Id] = new ChannelClient(VoiceChannel, AudioClient, StreamOut);

                await Play("Hello", Guild.Id, EAudioSource.Local);
            }
            catch
            {
                DiscordData.SendToChannel("C'è stato un errore nel Join del canale vocale", ECortanaChannels.Log);
            }
        }

        private static async Task Leave(SocketVoiceChannel VoiceChannel)
        {
            if (VoiceChannel == null) return;
            SocketGuild Guild = VoiceChannel.Guild;

            try
            {
                Clear(Guild.Id);
                DisposeConnection(Guild.Id);
                if (VoiceChannel == GetCurrentCortanaChannel(Guild)) await VoiceChannel.DisconnectAsync();
            }
            catch 
            {
                DiscordData.SendToChannel("C'è stato un errore nel Join del canale vocale", ECortanaChannels.Log);
            }
        }

        private static void DisposeConnection(ulong GuildID)
        {
            if (AudioClients.ContainsKey(GuildID))
            {
                AudioClients[GuildID].AudioStream?.Dispose();
                AudioClients[GuildID].AudioClient?.Dispose();
                AudioClients.Remove(GuildID);
            }
        }

        public static string Connect(SocketVoiceChannel Channel)
        {
            if (GetCurrentCortanaChannel(Channel.Guild) == Channel) return "Sono già qui";
            AddToJoinQueue(() => Join(Channel), Channel.Guild.Id);

            return "Arrivo";
        }

        public static string Disconnect(ulong GuildID)
        {
            foreach (var Client in AudioClients)
            {
                if (Client.Key == GuildID)
                {
                    AddToJoinQueue(() => Leave(Client.Value.VoiceChannel), GuildID);

                    return "Mi sto disconnettendo";
                }
            }
            return "Non sono connessa a nessun canale";
        }

        public static void EnsureChannel(SocketVoiceChannel? Channel)
        {
            if (Channel == null) return;
            if (IsConnected(Channel, Channel.Guild)) return;
            AddToJoinQueue(() => Join(Channel), Channel.Guild.Id);
        }

        //-------------------------------------------------------------------------------------------
    }

    [Group("media", "Gestione audio")]
    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("meme", "Metto un meme tra quelli disponibili", ignoreGroupNames: true)]
        public async Task Meme([Summary("nome", "Nome del meme")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.Si)
        {
            await DeferAsync(ephemeral: Ephemeral == EAnswer.Si);

            foreach(var Meme in DiscordData.Memes)
            {
                if (Meme.Value.Alias.Contains(name.ToLower()))
                {
                    string link = Meme.Value.Link;
                    string title = Meme.Key;

                    var result = await AudioHandler.GetYoutubeVideoInfos(link);
                    TimeSpan duration = result.Duration != null ? result.Duration.Value : TimeSpan.Zero;
                    Embed embed = DiscordData.CreateEmbed(title, Description: $"{duration:hh\\:mm\\:ss}");
                    embed = embed.ToEmbedBuilder()
                        .WithUrl(result.Url)
                        .WithThumbnailUrl(result.Thumbnails.Last().Url)
                        .Build();

                    await FollowupAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);

                    bool status = await AudioHandler.Play(result.Url, Context.Guild.Id, EAudioSource.Youtube);
                    if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il meme");
                    return;
                }
            }
            await FollowupAsync("Non ho nessun meme salvato con quel nome", ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("metti", "Metti qualcosa da youtube", runMode: RunMode.Async)]
        public async Task Play([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: Ephemeral == EAnswer.Si);

            var result = await AudioHandler.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration != null ? result.Duration.Value : TimeSpan.Zero;
            Embed embed = DiscordData.CreateEmbed(result.Title, Description: $"{duration:hh\\:mm\\:ss}");
            embed = embed.ToEmbedBuilder()
                .WithUrl(result.Url)
                .WithThumbnailUrl(result.Thumbnails.Last().Url)
                .Build();

            await FollowupAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);

            bool status = await AudioHandler.Play(result.Url, Context.Guild.Id, EAudioSource.Youtube);
            if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il video");
        }

        [SlashCommand("skippa", "Skippa quello che sto dicendo")]
        public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Skip(Context.Guild.Id);
            await RespondAsync(result, ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("ferma", "Rimuovi tutto quello che c'è in coda")]
        public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Clear(Context.Guild.Id);
            await RespondAsync(result, ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("connetti", "Entro nel canale dove sono stata chiamata")]
        public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            var Text = "Non posso connettermi se non sei in un canale";
            foreach(var voiceChannel in Context.Guild.VoiceChannels)
            {
                if (voiceChannel.ConnectedUsers.Contains(Context.User))
                {
                    Text = AudioHandler.Connect(voiceChannel);
                    break;
                }
            }
            await RespondAsync(Text, ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("disconnetti", "Esco dal canale vocale")]
        public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            var Text = AudioHandler.Disconnect(Context.Guild.Id);
            await RespondAsync(Text, ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("elenco-meme", "Lista dei meme disponibili", ignoreGroupNames: true)]
        public async Task GetMemes([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.Si)
        {
            Embed embed = DiscordData.CreateEmbed("Memes");
            var temp_embed = embed.ToEmbedBuilder();
            foreach (EMemeCategory category in Enum.GetValues(typeof(EMemeCategory)))
            {
                string categoryString = "";
                foreach(var meme in DiscordData.Memes)
                {
                    if (meme.Value.Category == category) categoryString += $"[{meme.Key}]({meme.Value.Link})\n";
                }
                if (categoryString.Length == 0) continue;
                temp_embed.AddField(category.ToString(), categoryString);
            }
            await RespondAsync(embed: temp_embed.Build(), ephemeral: Ephemeral == EAnswer.Si);
        }

        [SlashCommand("scarica-musica", "Scarica una canzone da youtube", runMode: RunMode.Async)]
        public async Task DownloadMusic([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: Ephemeral == EAnswer.Si);

            var result = await AudioHandler.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration != null ? result.Duration.Value : TimeSpan.Zero;
            Embed embed = DiscordData.CreateEmbed(result.Title, Description: $"{duration:hh\\:mm\\:ss}");
            embed = embed.ToEmbedBuilder()
            .WithDescription("Musica in download...")
            .WithUrl(result.Url)
            .WithThumbnailUrl(result.Thumbnails.Last().Url)
            .Build();

            await FollowupAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);

            var Stream = await AudioHandler.GetYoutubeAudioStream(result.Url);
            await Context.Channel.SendFileAsync(Stream, result.Title + ".mp3");
        }


        [SlashCommand("scarica-video", "Scarica un video da youtube", runMode: RunMode.Async)]
        public async Task DownloadVideo([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: Ephemeral == EAnswer.Si);

            var result = await AudioHandler.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration != null ? result.Duration.Value : TimeSpan.Zero;
            Embed embed = DiscordData.CreateEmbed(result.Title, Description: $"{duration:hh\\:mm\\:ss}");
            embed = embed.ToEmbedBuilder()
             .WithDescription("Video in download...")
            .WithUrl(result.Url)
            .WithThumbnailUrl(result.Thumbnails.Last().Url)
            .Build();

            await FollowupAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);

            var Stream = await AudioHandler.GetYoutubeVideoStream(result.Url);
            await Context.Channel.SendFileAsync(Stream, result.Title + ".mp4");
        }

        [SlashCommand("metti-file", "Metti un file mp3 in locale", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task PlayLocal([Summary("nome", "Nome del file")] EPrivateSounds sound, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.Si)
        {
            string name = sound switch
            {
                EPrivateSounds.Sorry => "Sorry",
                EPrivateSounds.Shutdown => "Shutdown",
                EPrivateSounds.Hello => "Hello",
                EPrivateSounds.Cortana1 => "Cortana_0",
                EPrivateSounds.Cortana2 => "Cortana_1",
                EPrivateSounds.Cortana3 => "Cortana_2",
                _ => "Hello"
            };
            await RespondAsync(embed: DiscordData.CreateEmbed("Metto " + name), ephemeral: Ephemeral == EAnswer.Si);

            bool status = await AudioHandler.Play(name, Context.Guild.Id, EAudioSource.Local);
            if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso far partire l'audio");
        }
    }
}
