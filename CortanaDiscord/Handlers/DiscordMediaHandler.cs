using CortanaDiscord.Utility;
using CortanaLib;
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
            _ = Task.Run(JoinQueue);
        }
    }

    private async Task JoinQueue()
    {
        try
        {
            while (true)
            {
                if (!_queue.TryDequeueLatest(out JoinAction? action, out CancellationTokenSource? token)) break;
                _currentJoinToken = token;

                if (action == null || token == null)
                {
                    token?.Dispose();
                    if (ReferenceEquals(_currentJoinToken, token)) _currentJoinToken = null;
                    continue;
                }

                try
                {
                    await JoinTask(action, token.Token);
                }
                catch (OperationCanceledException) { }
                finally
                {
                    token.Dispose();
                    if (ReferenceEquals(_currentJoinToken, token)) _currentJoinToken = null;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isPlaying, 0);
            if (_queue.HasNext() && Interlocked.CompareExchange(ref _isPlaying, 1, 0) == 0)
            {
                _ = Task.Run(JoinQueue);
            }
        }
    }

    private async Task JoinTask(JoinAction joinAction, CancellationToken cancellationToken)
    {
        try
        {
            switch (joinAction.Status)
            {
                case JoinStatus.Join:
                    {
                        SocketVoiceChannel channel = joinAction.Channel!;
                        DataHandler.Log($"Join requested for channel {channel.Name} ({channel.Id})");
                        await Task.Delay(1500, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!AudioHandler.GetAvailableChannels(channel.Guild).Any(availableChannel => availableChannel.Id == channel.Id)) return;
                        if (AudioHandler.IsConnected(channel, guild)) return;

                        await AudioHandler.Stop(guild.Id);
                        cancellationToken.ThrowIfCancellationRequested();

                        IAudioClient? audioClient = await channel.ConnectAsync();
                        if (audioClient == null) throw new CortanaException("Errore connessione al canale vocale");
                        DataHandler.Log($"Connected to channel {channel.Name} ({channel.Id})");

                        CurrentChannel = channel;

                        MediaPlayer?.Dispose();
                        MediaPlayer = new DiscordMediaPlayer(audioClient);

                        await Task.Delay(1800, cancellationToken);
                        if (GetActualConnectedChannel(guild)?.Id == channel.Id)
                        {
                            AudioHandler.SayHello(guild.Id);
                        }
                        break;
                    }
                case JoinStatus.Leave:
                    {
                        DataHandler.Log($"Leave requested");
                        cancellationToken.ThrowIfCancellationRequested();
                        await AudioHandler.Stop(guild.Id);
                        MediaPlayer?.Dispose();
                        SocketVoiceChannel? actualChannel = GetActualConnectedChannel(guild);
                        if (CurrentChannel != null && actualChannel != null && CurrentChannel.Id == actualChannel.Id)
                        {
                            await CurrentChannel.DisconnectAsync();
                        }
                        CurrentChannel = null;
                        DataHandler.Log($"Leave completed");
                        break;
                    }
            }
        }
        catch (OperationCanceledException)
        {

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