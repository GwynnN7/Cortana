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
        private static Dictionary<ulong, CancellationTokenSource> JoinRegulators = new Dictionary<ulong, CancellationTokenSource>();
        public static Dictionary<ulong, ChannelClient> AudioClients = new Dictionary<ulong, ChannelClient>();
        
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

        private static bool ShouldCortanaStay(SocketGuild Guild)
        {
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID) && voiceChannel.Users.Count > 1) return true;
            }
            return false;
        }

        public static SocketVoiceChannel? GetAvailableChannel(SocketGuild Guild)
        {
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.Users.Count > 0 && !voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID) && voiceChannel.Id != DiscordData.DiscordIDs.GulagID) return voiceChannel;
            }
            return Guild.Id == DiscordData.DiscordIDs.NoMenID ? Guild.GetVoiceChannel(DiscordData.DiscordIDs.CortanaChannelID) : null;
        }

        private static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild Guild)
        {
            List<SocketVoiceChannel> channels = new List<SocketVoiceChannel>();
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.Users.Count > 0 && !voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID) && voiceChannel.Id != DiscordData.DiscordIDs.GulagID) channels.Add(voiceChannel);
            }
            if(Guild.Id == DiscordData.DiscordIDs.NoMenID) if (channels.Count == 0) channels.Add(Guild.GetVoiceChannel(DiscordData.DiscordIDs.CortanaChannelID));
            return channels;
        }

        private static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild Guild)
        {
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) return voiceChannel;
            }
            return null;
        }

        public static void TryConnection(SocketGuild Guild)
        {
            if (ShouldCortanaStay(Guild)) Console.WriteLine("I SHOULD STAY");
            foreach (var voiceChannel in Guild.VoiceChannels)
            {
                if (voiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) Console.WriteLine($"Cortana in {voiceChannel.Name}");
            }
            if (!ShouldCortanaStay(Guild))
            {
                if (DiscordData.GuildSettings[Guild.Id].AutoJoin)
                {
                    var channel = GetAvailableChannel(Guild);
                    if (channel == null) Disconnect(Guild.Id);
                    else JoinChannel(channel);
                }
                else Disconnect(Guild.Id);
            }
            else EnsureChannel(GetCurrentCortanaChannel(Guild));
        }

        private static async Task ConnectToVoice(SocketVoiceChannel VoiceChannel)
        {
            if (VoiceChannel == null) return;
            ulong GuildID = VoiceChannel.Guild.Id;
            try
            {
                await Task.Delay(1500);
                if (!GetAvailableChannels(VoiceChannel.Guild).Contains(VoiceChannel))
                {
                    TryConnection(VoiceChannel.Guild);
                    return;
                }

                DisposeConnection(GuildID);

                var NewPair = new ChannelClient(VoiceChannel);
                AudioClients.Add(GuildID, NewPair);

                var AudioClient = await VoiceChannel.ConnectAsync();
                var StreamOut = AudioClient.CreatePCMStream(AudioApplication.Mixed, 64000, packetLoss: 0);
                AudioClients[GuildID] = new ChannelClient(VoiceChannel, AudioClient, StreamOut);

                await Play("Hello", GuildID, EAudioSource.Local);
                await Play("Cortana_1", GuildID, EAudioSource.Local);
            }
            catch (OperationCanceledException){}
            finally
            {
                DisposeJoinRegulator(GuildID);
            }
        }

        private static async Task DisconnectFromVoice(SocketVoiceChannel VoiceChannel)
        {
            if (VoiceChannel == null) return;
            ulong GuildID = VoiceChannel.Guild.Id;

            try
            {
                DisposeConnection(GuildID);

                if (VoiceChannel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) await VoiceChannel.DisconnectAsync();
            }
            catch (OperationCanceledException) {}
            finally 
            { 
                DisposeJoinRegulator(GuildID); 
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
            DisposeJoinRegulator(Channel.Guild.Id);
            JoinRegulators.Add(Channel.Guild.Id, new CancellationTokenSource());
            Task.Run(() => ConnectToVoice(Channel), JoinRegulators[Channel.Guild.Id].Token);
            return Text;
        }

        public static string Disconnect(ulong GuildID)
        {
            foreach (var Client in AudioClients)
            {
                if (Client.Key == GuildID)
                {
                    DisposeJoinRegulator(GuildID);
                    JoinRegulators.Add(GuildID, new CancellationTokenSource());
                    Task.Run(() => DisconnectFromVoice(Client.Value.VoiceChannel), JoinRegulators[GuildID].Token);
                    return "Mi sto disconnettendo";
                }
            }
            return "Non sono connessa a nessun canale";
        }

        public static void EnsureChannel(SocketVoiceChannel? Channel)
        {
            if (Channel == null) return;
            if (AudioClients.ContainsKey(Channel.Guild.Id) && AudioClients[Channel.Guild.Id].VoiceChannel.Id == Channel.Id && Channel.Users.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) return;
            JoinChannel(Channel);
        }

        private static void DisposeJoinRegulator(ulong GuildID)
        {
            if (JoinRegulators.ContainsKey(GuildID))
            {
                Clear(GuildID);
                JoinRegulators[GuildID].Cancel();
                JoinRegulators[GuildID].Dispose();
                JoinRegulators.Remove(GuildID);
            }
        }
    }

    [Group("media", "Gestione audio")]
    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
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
                if (voiceChannel.Users.Contains(Context.User))
                {
                    Text = AudioHandler.JoinChannel(voiceChannel);
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

        [SlashCommand("cerca-video", "Cerca un video su youtube", runMode: RunMode.Async)]
        public async Task SearchVideo([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.No)
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
        }

        [SlashCommand("metti-in-locale", "Metti qualcosa in locale", runMode: RunMode.Async)]
        [RequireOwner]
        public async Task PlayLocal([Summary("file", "Nome del file")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer Ephemeral = EAnswer.Si)
        {
            await RespondAsync(embed: DiscordData.CreateEmbed("Metto " + text), ephemeral: Ephemeral == EAnswer.Si);

            bool status = await AudioHandler.Play(text, Context.Guild.Id, EAudioSource.Local);
            if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso far partire l'audio");
        }

        [SlashCommand("resetta-connessione", "Resetto la connessione al canale vocale", runMode: RunMode.Async)]
        public async Task ResetConnection()
        {
            await RespondAsync(embed: DiscordData.CreateEmbed("Procedo a resettare la connessione"), ephemeral: true);
            AudioHandler.TryConnection(Context.Guild);
        }
    }
}
