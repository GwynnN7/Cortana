using System.Diagnostics;
using CortanaLib;
using Discord.Audio;
namespace CortanaDiscord.Handlers;

public class DiscordQueue<T> where T : class{
    private readonly Queue<T> _queue = new();
    private readonly Queue<CancellationTokenSource> _tokens = new();
    private readonly Lock _lock = new();

    public void Enqueue(T track) {
        lock (_lock)
        {
            _queue.Enqueue(track);
            _tokens.Enqueue(new CancellationTokenSource());
        }
    }

    public T? Dequeue() {
        lock (_lock) {
            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
    }
    
    public CancellationTokenSource? DequeueToken() {
        lock (_lock) {
            return _tokens.Count > 0 ? _tokens.Dequeue() : null;
        }
    }

    public void Clear() {
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
    
    public bool HasNext() {
        lock (_lock) {
            return _queue.Count > 0;
        }
    }
}

public class DiscordMediaPlayer(IAudioClient client)
{
    private readonly DiscordQueue<AudioTrack> _queue = new();
    private CancellationTokenSource? _currentTrackToken;
    
    private volatile bool _isPlaying;

    public void Enqueue(AudioTrack track)
    {
        _queue.Enqueue(track);
        if (!_isPlaying)
        {
            _isPlaying = true;
            Task.Run(PlayQueue);
        }
    }
    
    public async Task<bool> Skip()
    {
        if (_currentTrackToken == null) return false;
        await _currentTrackToken.CancelAsync();
        return true;
    }

    public bool Clear()
    {
        if (!_queue.HasNext()) return false;
        _queue.Clear();
        return true;
    }

    public void Dispose()
    {
        client.Dispose();
    }
    
    private async Task PlayQueue()
    {
        while (_queue.HasNext())
        {
            AudioTrack? track = _queue.Dequeue();
            _currentTrackToken = _queue.DequeueToken()!;
            if (track == null || string.IsNullOrWhiteSpace(track.StreamUrl))
            {
                _currentTrackToken.Dispose();
                _currentTrackToken = null;
                continue;
            }

            using Process ffmpeg = CreateStream(track.StreamUrl);
            await using Stream output = ffmpeg.StandardOutput.BaseStream;
            await using AudioOutStream? discord = client.CreatePCMStream(AudioApplication.Mixed);
            
            try
            {
                await output.CopyToAsync(discord, _currentTrackToken.Token);
            }
            finally
            {
                await discord.FlushAsync();
                _currentTrackToken.Dispose();
                _currentTrackToken = null;
            }
        }

        _isPlaying = false;
    }
    
    private static Process CreateStream(string path)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = "ffmpeg",
            Arguments = $"-hide_banner -loglevel panic -i \"{path}\" -ac 2 -f s16le -ar 48000 pipe:1",
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;
    }
}