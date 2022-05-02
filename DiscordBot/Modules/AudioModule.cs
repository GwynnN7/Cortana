using CliWrap;
using Discord.Audio;
using Discord.Interactions;
using Discord.WebSocket;
using YoutubeExplode;
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
            public MemoryStream Data { get; }
            public CancellationTokenSource Token { get; }
            public ulong GuildID { get; }
            public Task? CurrentTask { get; set; } = null;
            public Task? TaskToAwait { get; } = null;

            public QueueStructure(MemoryStream NewStream, CancellationTokenSource NewToken, ulong NewGuildID, Task? NewTaskToAwait)
            {
                Data = NewStream;
                Token = NewToken;
                GuildID = NewGuildID;
                TaskToAwait = NewTaskToAwait;
            }
        }

        private static Dictionary<ulong, List<QueueStructure>> Queue = new Dictionary<ulong, List<QueueStructure>>();
        public static Dictionary<ulong, ChannelClient> AudioClients = new Dictionary<ulong, ChannelClient>();
        private static CancellationTokenSource? JoinRegulator = null;
        
        private static async Task<MemoryStream> ExecuteFFMPEG(Stream? VideoStream = null, string FilePath = "")
        {
            var memoryStream = new MemoryStream();
            await Cli.Wrap("ffmpeg")
                .WithArguments($" -hide_banner -loglevel panic -i {(VideoStream != null ? "pipe:0" : $"\"{FilePath}\"")} -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe((VideoStream != null ? PipeSource.FromStream(VideoStream) : PipeSource.Null))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync();
            return memoryStream;
        }

        private static async Task<Stream> GetYoutubeAudioStream(string url)
        {
            YoutubeClient youtube = new YoutubeClient();
            var StreamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            var StreamInfo = StreamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            var Stream = await youtube.Videos.Streams.GetAsync(StreamInfo);
            return Stream;
        }

        public static async Task<YoutubeExplode.Videos.Video> GetYoutubeVideoInfos(string url) => await new YoutubeClient().Videos.GetAsync(url);

        private static async Task ConnectToVoice(SocketVoiceChannel VoiceChannel)
        {
            try
            {
                await Task.Delay(500);
                if (VoiceChannel == null) return;

                ulong GuildID = VoiceChannel.Guild.Id;
                DisposeConnection(GuildID);

                var NewPair = new ChannelClient(VoiceChannel);
                AudioClients.Add(GuildID, NewPair);

                var AudioClient = await VoiceChannel.ConnectAsync();
                var StreamOut = AudioClient.CreatePCMStream(AudioApplication.Mixed, 64000, packetLoss: 0);
                AudioClients[GuildID] = new ChannelClient(VoiceChannel, AudioClient, StreamOut);

                await Play("Hello", GuildID, EAudioSource.Local);
                await Play("Cortana_1", GuildID, EAudioSource.Local);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("ciao");
            }
            finally
            {
                DisposeJoinRegulator();
            }
        }

        private static async Task DisconnectFromVoice(SocketVoiceChannel VoiceChannel)
        {
            try
            {
                if (VoiceChannel == null) return;

                ulong GuildID = VoiceChannel.Guild.Id;
                DisposeConnection(GuildID);

                if (VoiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) await VoiceChannel.DisconnectAsync();
            }
            catch (OperationCanceledException) 
            {
                Console.WriteLine("ciao2");
            }
            finally { 
                DisposeJoinRegulator(); 
            }
        }

        private static void DisposeConnection(ulong GuildID)
        {
            if (AudioClients.ContainsKey(GuildID))
            {
                if (AudioClients[GuildID].AudioStream != null) AudioClients[GuildID].AudioStream.Dispose();
                if (AudioClients[GuildID].AudioClient != null) AudioClients[GuildID].AudioClient.Dispose();
                AudioClients.Remove(GuildID);
            }
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

            if (!Queue.ContainsKey(GuildID)) Queue.Add(GuildID, new List<QueueStructure>());
            var AudioQueueItem = new QueueStructure(MemoryStream, new CancellationTokenSource(), GuildID, Queue[GuildID].Count > 0 ? Queue[GuildID].Last().CurrentTask : null);
            var AudioTask = Task.Run(() => SendBuffer(AudioQueueItem));
            AudioQueueItem.CurrentTask = AudioTask;
            Queue[GuildID].Add(AudioQueueItem);

            return true;
        }

        public static string Skip(ulong GuildID)
        {
            if (Queue.ContainsKey(GuildID))
            {
                if (Queue[GuildID].Count > 0)
                {
                    Queue[GuildID].First().Token.Cancel(true);
                    return "Audio skippato";
                }
            }
            return "Non c'è niente da skippare";
        }

        public static string Clear(ulong GuildID)
        {
            if (Queue.ContainsKey(GuildID))
            {
                bool HasStopped = false;
                Queue[GuildID].Reverse();
                foreach (var QueueItem in Queue[GuildID])
                {
                    HasStopped = true;
                    QueueItem.Token.Cancel(true);
                }
                if(HasStopped) return "Queue rimossa";
            }
            return "Non c'è niente in coda";
        }

        private static async Task SendBuffer(QueueStructure AudioQueueItem)
        {
            if (AudioQueueItem.TaskToAwait != null) await AudioQueueItem.TaskToAwait;
            try
            {
                await AudioClients[AudioQueueItem.GuildID].AudioStream.WriteAsync(AudioQueueItem.Data.GetBuffer(), AudioQueueItem.Token.Token);
            }
            catch(OperationCanceledException)
            {
                await AudioClients[AudioQueueItem.GuildID].AudioStream.FlushAsync();
            }
            finally
            {
                Queue[AudioQueueItem.GuildID].RemoveAt(0);
                AudioQueueItem.Token.Dispose();
            }
        }

        public static string JoinChannel(SocketVoiceChannel Channel)
        {
            string Text = "Arrivo";
            foreach(var Client in AudioClients)
            {
                if(Client.Key == Channel.Guild.Id)
                {
                    if (Client.Value.VoiceChannel.Id == Channel.Id) return "Sono già qui"; 
                    break;
                }
            }
            DisposeJoinRegulator();
            JoinRegulator = new CancellationTokenSource();
            Task.Run(() => ConnectToVoice(Channel), JoinRegulator.Token);
            return Text;
        }

        public static string Disconnect(ulong GuildID)
        {
            foreach (var Client in AudioClients)
            {
                if (Client.Key == GuildID)
                {
                    DisposeJoinRegulator();
                    JoinRegulator = new CancellationTokenSource();
                    Task.Run(() => DisconnectFromVoice(Client.Value.VoiceChannel), JoinRegulator.Token);
                    return "Mi sto disconnettendo";
                }
            }
            return "Non sono connessa a nessun canale";
        }

        public static void EnsureChannel(SocketVoiceChannel? Channel)
        {
            if (Channel == null) return;
            if (AudioClients.ContainsKey(Channel.Guild.Id) && AudioClients[Channel.Guild.Id].VoiceChannel == Channel) return;
            JoinChannel(Channel);
        }

        private static void DisposeJoinRegulator()
        {
            if (JoinRegulator != null)
            {
                JoinRegulator.Cancel();
                JoinRegulator.Dispose();
                JoinRegulator = null;
            }
        }
    }

    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
        [Group("audio", "Gestione audio")]
        public class Audio : InteractionModuleBase<SocketInteractionContext>
        {
            [SlashCommand("connetti", "Entro nel canale dove sono stata chiamata")]
            public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                var Text = "C'è stato un problema :/";
                Context.Guild.VoiceChannels.ToList().ForEach(x =>
                {
                    if (x.Users.Contains(Context.User))
                    {
                        Text = AudioHandler.JoinChannel(x);
                        return;
                    }
                }
                );
                await RespondAsync(Text, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("disconnetti", "Esco dal canale vocale")]
            public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                var Text = AudioHandler.Disconnect(Context.Guild.Id);
                await RespondAsync(Text, ephemeral: Ephemeral == EAnswer.Si);
            }

            [SlashCommand("metti", "Metti qualcosa da youtube", runMode: RunMode.Async)]
            public async Task Play([Summary("url", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
            {
                var result = await AudioHandler.GetYoutubeVideoInfos(text);
                TimeSpan duration = result.Duration != null ? result.Duration.Value : TimeSpan.Zero;
                Embed embed = DiscordData.CreateEmbed(result.Title, Description: $"{duration:hh\\:mm\\:ss}");
                embed = embed.ToEmbedBuilder()
                .WithUrl(result.Url)
                .WithThumbnailUrl(result.Thumbnails.Last().Url)
                .Build();

                await RespondAsync(embed: embed, ephemeral: Ephemeral == EAnswer.Si);

                bool status = await AudioHandler.Play(result.Url, Context.Guild.Id, EAudioSource.Youtube);
                if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il video");
                
            }

            [SlashCommand("metti-in-locale", "Metti qualcosa in locale", runMode: RunMode.Async)]
            [RequireOwner]
            public async Task PlayLocal([Summary("file", "Nome del file")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.Si)
            {
                await RespondAsync(embed: DiscordData.CreateEmbed("Metto " + text), ephemeral: Ephemeral == EAnswer.Si);

                bool status = await AudioHandler.Play(text, Context.Guild.Id, EAudioSource.Local);
                if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso far partire l'audio");
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
        }
    }
}
