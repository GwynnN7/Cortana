using System.Collections.Concurrent;
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

public class DiscordMediaHandler(SocketGuild guild) : IDisposable
{
    private readonly ConcurrentQueue<JoinAction> _queue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _disposeToken = new();
    private Task? _workerTask;
    private bool _disposed;

    public SocketVoiceChannel? CurrentChannel;
    public DiscordMediaPlayer? MediaPlayer;

    public void Enqueue(JoinAction joinAction)
    {
        lock (_stateLock)
        {
            if (_disposed) return;

            _queue.Enqueue(joinAction);
            _queueSignal.Release();
            EnsureWorkerLocked();
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _disposeToken.Cancel();

        MediaPlayer?.Dispose();
        MediaPlayer = null;
        CurrentChannel = null;

        while (_queue.TryDequeue(out _)) { }

        _queueSignal.Dispose();
        _disposeToken.Dispose();
    }

    private void EnsureWorkerLocked()
    {
        if (_workerTask is { IsCompleted: false }) return;
        _workerTask = Task.Run(JoinQueueAsync);
    }

    private async Task JoinQueueAsync()
    {
        while (!_disposeToken.Token.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(_disposeToken.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_queue.TryDequeue(out JoinAction? action) || action == null) continue;

            // Collapse bursts of stale actions and apply only the most recent intent.
            while (_queue.TryDequeue(out JoinAction? latestAction))
            {
                if (latestAction != null) action = latestAction;
            }

            try
            {
                await JoinTask(action, _disposeToken.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                DataHandler.Log($"JoinQueue error: {ex.Message}");
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
                        if (joinAction.Channel == null) return;

                        SocketVoiceChannel channel = joinAction.Channel;
                        DataHandler.Log($"Join requested for channel {channel.Name} ({channel.Id})");

                        if (!AudioHandler.GetAvailableChannels(channel.Guild).Any(availableChannel => availableChannel.Id == channel.Id)) return;
                        if (AudioHandler.IsConnected(channel, guild) && MediaPlayer != null)
                        {
                            CurrentChannel = channel;
                            return;
                        }

                        await DisconnectInternal(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();

                        IAudioClient? audioClient = await channel.ConnectAsync();
                        if (audioClient == null)
                            throw new CortanaException("Errore connessione al canale vocale");

                        DataHandler.Log($"Connected to channel {channel.Name} ({channel.Id})");

                        CurrentChannel = channel;
                        MediaPlayer?.Dispose();
                        MediaPlayer = new DiscordMediaPlayer(audioClient);

                        await Task.Delay(1300, cancellationToken);
                        if (GetActualConnectedChannel(guild)?.Id == channel.Id)
                        {
                            AudioHandler.SayHello(guild.Id);
                        }

                        break;
                    }
                case JoinStatus.Leave:
                    {
                        DataHandler.Log("Leave requested");
                        await DisconnectInternal(cancellationToken);
                        DataHandler.Log("Leave completed");
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

    private async Task DisconnectInternal(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        DiscordMediaPlayer? player = MediaPlayer;
        MediaPlayer = null;

        if (player != null)
        {
            player.Clear();
            await player.Skip();
            player.Dispose();
        }

        SocketVoiceChannel? actualChannel = GetActualConnectedChannel(guild);
        SocketVoiceChannel? previousChannel = CurrentChannel;
        CurrentChannel = null;

        SocketVoiceChannel? channelToLeave = actualChannel ?? previousChannel;
        if (channelToLeave == null) return;

        cancellationToken.ThrowIfCancellationRequested();
        await channelToLeave.DisconnectAsync();
    }

    private static SocketVoiceChannel? GetActualConnectedChannel(SocketGuild guild)
    {
        return guild.VoiceChannels.FirstOrDefault(voiceChannel => voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordUtils.Data.CortanaId));
    }
}