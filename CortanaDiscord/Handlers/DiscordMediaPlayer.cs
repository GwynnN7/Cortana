using System.Collections.Concurrent;
using System.Diagnostics;
using CortanaLib;
using CortanaLib.Structures;
using Discord.Audio;

namespace CortanaDiscord.Handlers;

public class DiscordMediaPlayer(IAudioClient client) : IDisposable
{
    private readonly ConcurrentQueue<AudioTrack> _queue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly object _stateLock = new();
    private readonly CancellationTokenSource _disposeToken = new();
    private CancellationTokenSource? _currentTrackToken;
    private Task? _workerTask;
    private int _connectionWarmupDone;
    private int _voicePipelinePrimed;
    private bool _disposed;

    public void Enqueue(AudioTrack track)
    {
        if (string.IsNullOrWhiteSpace(track.StreamUrl)) return;

        lock (_stateLock)
        {
            if (_disposed) return;
            _queue.Enqueue(track);
            _queueSignal.Release();
            EnsureWorkerLocked();
        }
    }

    public Task<bool> Skip()
    {
        CancellationTokenSource? trackToken;
        lock (_stateLock)
        {
            if (_disposed) return Task.FromResult(false);
            trackToken = _currentTrackToken;
        }

        if (trackToken == null) return Task.FromResult(false);

        try
        {
            trackToken.Cancel();
            return Task.FromResult(true);
        }
        catch (ObjectDisposedException)
        {
            return Task.FromResult(false);
        }
    }

    public bool Clear()
    {
        if (_disposed) return false;

        var removed = false;
        while (_queue.TryDequeue(out _))
        {
            removed = true;
        }
        return removed;
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed) return;
            _disposed = true;
        }

        _disposeToken.Cancel();

        CancellationTokenSource? currentToken;
        lock (_stateLock)
        {
            currentToken = _currentTrackToken;
            _currentTrackToken = null;
        }

        try
        {
            currentToken?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }

        currentToken?.Dispose();
        while (_queue.TryDequeue(out _)) { }

        _queueSignal.Dispose();
        _disposeToken.Dispose();
        client.Dispose();
    }

    private void EnsureWorkerLocked()
    {
        if (_workerTask is { IsCompleted: false }) return;
        _workerTask = Task.Run(ProcessQueueAsync);
    }

    private async Task ProcessQueueAsync()
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

            if (!_queue.TryDequeue(out AudioTrack? track)) continue;
            if (track == null || string.IsNullOrWhiteSpace(track.StreamUrl)) continue;

            using var trackToken = CancellationTokenSource.CreateLinkedTokenSource(_disposeToken.Token);
            lock (_stateLock)
            {
                if (_disposed) return;
                _currentTrackToken = trackToken;
            }

            try
            {
                if (Interlocked.CompareExchange(ref _connectionWarmupDone, 1, 0) == 0)
                {
                    await Task.Delay(1200, trackToken.Token);
                }

                await PlayTrackAsync(track, trackToken.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                DataHandler.Log($"Playback error: {ex.Message}");
            }
            finally
            {
                lock (_stateLock)
                {
                    if (ReferenceEquals(_currentTrackToken, trackToken)) _currentTrackToken = null;
                }
            }
        }
    }

    private async Task PlayTrackAsync(AudioTrack track, CancellationToken cancellationToken)
    {
        DataHandler.Log($"Starting track: {track.Title}");

        if (Interlocked.CompareExchange(ref _voicePipelinePrimed, 1, 0) == 0)
        {
            await PrimeVoicePipelineAsync(cancellationToken);
        }

        using Process ffmpeg = CreateStream(track.StreamUrl);

        try
        {
            await client.SetSpeakingAsync(true);

            await using Stream ffmpegStream = ffmpeg.StandardOutput.BaseStream;
            await using AudioOutStream discord = client.CreatePCMStream(AudioApplication.Mixed);

            try
            {
                await ffmpegStream.CopyToAsync(discord, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                try
                {
                    await discord.FlushAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            try
            {
                await client.SetSpeakingAsync(false);
            }
            catch (Exception)
            {
            }

            string ffmpegError = await ffmpeg.StandardError.ReadToEndAsync();

            if (!ffmpeg.HasExited)
            {
                try
                {
                    ffmpeg.Kill(true);
                }
                catch (Exception)
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(ffmpegError))
            {
                DataHandler.Log($"ffmpeg stderr: {ffmpegError.Trim()}");
            }

            DataHandler.Log($"Track finished: {track.Title}");
        }
    }

    private async Task PrimeVoicePipelineAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.SetSpeakingAsync(true);

            await using AudioOutStream discord = client.CreatePCMStream(AudioApplication.Mixed);

            // 20 ms of stereo 16-bit PCM at 48 kHz is 3840 bytes.
            byte[] silenceFrame = new byte[3840];
            for (var i = 0; i < 120; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await discord.WriteAsync(silenceFrame, cancellationToken);
                await Task.Delay(20, cancellationToken);
            }

            await discord.FlushAsync(cancellationToken);
            DataHandler.Log("Voice pipeline primed");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            DataHandler.Log($"Voice warmup error: {ex.Message}");
        }
        finally
        {
            try
            {
                await client.SetSpeakingAsync(false);
            }
            catch (Exception)
            {
            }
        }
    }

    private static Process CreateStream(string path)
    {
        Process? ffmpeg = Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -nostdin -rw_timeout 15000000 -i \"{path}\" -vn -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });

        if (ffmpeg == null)
            throw new CortanaException("Unable to start ffmpeg process");

        return ffmpeg;
    }
}