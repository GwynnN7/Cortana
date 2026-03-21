using System.Diagnostics;
using CortanaLib;
using Discord.Audio;
namespace CortanaDiscord.Handlers;

public class DiscordQueue<T> where T : class
{
    private readonly Queue<T> _queue = new();
    private readonly Queue<CancellationTokenSource> _tokens = new();
    private readonly Lock _lock = new();

    public void Enqueue(T track)
    {
        lock (_lock)
        {
            _queue.Enqueue(track);
            _tokens.Enqueue(new CancellationTokenSource());
        }
    }

    public bool TryDequeue(out T? item, out CancellationTokenSource? token)
    {
        lock (_lock)
        {
            if (_queue.Count == 0 || _tokens.Count == 0)
            {
                item = null;
                token = null;
                return false;
            }

            item = _queue.Dequeue();
            token = _tokens.Dequeue();
            return true;
        }
    }

    public bool TryDequeueLatest(out T? item, out CancellationTokenSource? token)
    {
        lock (_lock)
        {
            if (_queue.Count == 0 || _tokens.Count == 0)
            {
                item = null;
                token = null;
                return false;
            }

            while (_queue.Count > 1 && _tokens.Count > 1)
            {
                _queue.Dequeue();
                _tokens.Dequeue().Dispose();
            }

            item = _queue.Dequeue();
            token = _tokens.Dequeue();
            return true;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            foreach (CancellationTokenSource token in _tokens)
            {
                token.Dispose();
            }
            _queue.Clear();
            _tokens.Clear();
        }
    }

    public bool HasNext()
    {
        lock (_lock)
        {
            return _queue.Count > 0;
        }
    }
}

public class DiscordMediaPlayer(IAudioClient client) : IDisposable
{
    private readonly DiscordQueue<AudioTrack> _queue = new();
    private CancellationTokenSource? _currentTrackToken;

    private int _isPlaying;

    public void Enqueue(AudioTrack track)
    {
        _queue.Enqueue(track);
        if (Interlocked.CompareExchange(ref _isPlaying, 1, 0) == 0)
        {
            _ = Task.Run(PlayQueue);
        }
    }

    public Task<bool> Skip()
    {
        CancellationTokenSource? token = _currentTrackToken;
        if (token == null) return Task.FromResult(false);

        try
        {
            token.Cancel();
            return Task.FromResult(true);
        }
        catch (ObjectDisposedException)
        {
            return Task.FromResult(false);
        }
    }

    public bool Clear()
    {
        if (!_queue.HasNext()) return false;
        _queue.Clear();
        return true;
    }

    public void Dispose()
    {
        _currentTrackToken?.Cancel();
        _currentTrackToken?.Dispose();
        _currentTrackToken = null;
        _queue.Clear();
        client.Dispose();
    }

    private async Task PlayQueue()
    {
        try
        {
            while (true)
            {
                if (!_queue.TryDequeue(out AudioTrack? track, out CancellationTokenSource? token)) break;
                _currentTrackToken = token;

                if (track == null || string.IsNullOrWhiteSpace(track.StreamUrl) || token == null)
                {
                    token?.Dispose();
                    if (ReferenceEquals(_currentTrackToken, token)) _currentTrackToken = null;
                    continue;
                }

                DataHandler.Log($"Starting track: {track.Title}");

                Process ffmpeg = CreateStream(track.StreamUrl);
                try
                {
                    await using Stream output = ffmpeg.StandardOutput.BaseStream;
                    await using AudioOutStream? discord = client.CreatePCMStream(AudioApplication.Mixed);

                    try
                    {
                        await output.CopyToAsync(discord, token.Token);
                    }
                    catch (OperationCanceledException) { }
                    finally
                    {
                        await discord.FlushAsync();
                    }
                }
                catch (Exception ex)
                {
                    DataHandler.Log($"Playback error: {ex.Message}");
                }
                finally
                {
                    if (!ffmpeg.HasExited) ffmpeg.Kill(true);
                    ffmpeg.Dispose();

                    DataHandler.Log($"Track finished: {track.Title}");

                    token.Dispose();
                    if (ReferenceEquals(_currentTrackToken, token)) _currentTrackToken = null;
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isPlaying, 0);
            if (_queue.HasNext() && Interlocked.CompareExchange(ref _isPlaying, 1, 0) == 0)
            {
                _ = Task.Run(PlayQueue);
            }
        }
    }

    private static Process CreateStream(string path)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -nostdin -rw_timeout 15000000 -i \"{path}\" -vn -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
    }
}