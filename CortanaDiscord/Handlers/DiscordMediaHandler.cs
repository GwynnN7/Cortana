using CortanaDiscord.Utility;
using CortanaLib.Structures;
using Discord.Audio;
using Discord.WebSocket;

namespace CortanaDiscord.Handlers;

public enum JoinStatus
{
    Join,
    Leave
}
public record JoinAction(JoinStatus Status, SocketVoiceChannel? Channel);

public class DiscordMediaHandler(SocketGuild guild)
{
    private readonly DiscordQueue<JoinAction> _queue = new();
    private CancellationTokenSource? _currentJoinToken;

    private int _isPlaying;

    public SocketVoiceChannel? CurrentChannel;
    public DiscordMediaPlayer? MediaPlayer;

    public void Enqueue(JoinAction joinAction)
    {
        _queue.Enqueue(joinAction);
        if (Interlocked.CompareExchange(ref _isPlaying, 1, 0) == 0)
        {
            Task.Run(JoinQueue);
        }
    }

    private void EnqueueLast()
    {
        JoinAction? joinAction = null;
        while (_queue.HasNext())
        {
            joinAction = _queue.Dequeue();
        }
        if (joinAction == null) return;
        Enqueue(joinAction);
    }

    private async Task JoinQueue()
    {
        try
        {
            while (_queue.HasNext())
            {
                JoinAction? action = _queue.Dequeue();
                CancellationTokenSource? token = _queue.DequeueToken();
                _currentJoinToken = token;

                if (action == null || token == null)
                {
                    token?.Dispose();
                    if (ReferenceEquals(_currentJoinToken, token)) _currentJoinToken = null;
                    continue;
                }

                try
                {
                    await Task.Run(() => JoinTask(action), token.Token);
                }
                catch (OperationCanceledException) { }
                finally
                {
                    token.Dispose();
                    if (ReferenceEquals(_currentJoinToken, token)) _currentJoinToken = null;
                }

                EnqueueLast();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isPlaying, 0);
        }
    }

    private async Task JoinTask(JoinAction joinAction)
    {
        try
        {
            switch (joinAction.Status)
            {
                case JoinStatus.Join:
                    {
                        SocketVoiceChannel channel = joinAction.Channel!;
                        await Task.Delay(1500);

                        if (!AudioHandler.GetAvailableChannels(channel.Guild).Contains(channel)) return;
                        if (AudioHandler.IsConnected(channel, guild)) return;

                        await AudioHandler.Stop(guild.Id);

                        IAudioClient? audioClient = await channel.ConnectAsync();
                        if (audioClient == null) throw new CortanaException("Errore connessione al canale vocale");

                        CurrentChannel = channel;

                        MediaPlayer?.Dispose();
                        MediaPlayer = new DiscordMediaPlayer(audioClient);

                        AudioHandler.SayHello(guild.Id);
                        break;
                    }
                case JoinStatus.Leave:
                    {
                        await AudioHandler.Stop(guild.Id);
                        MediaPlayer?.Dispose();
                        if (CurrentChannel == GetActualConnectedChannel(guild) && CurrentChannel != null)
                        {
                            await CurrentChannel.DisconnectAsync();
                        }
                        CurrentChannel = null;
                        break;
                    }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore nella gestione della coda di join: {ex.Message}");
            await DiscordUtils.SendToChannel("Errore con la gestione della coda di join", ECortanaChannels.Log);
        }
    }

    private static SocketVoiceChannel? GetActualConnectedChannel(SocketGuild guild)
    {
        return guild.VoiceChannels.FirstOrDefault(voiceChannel => voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordUtils.Data.CortanaId));
    }
}