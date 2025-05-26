using System.Diagnostics;
using CliWrap;
using CortanaLib;
using Discord.Audio;
using Discord.WebSocket;

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
    private CancellationTokenSource _cts = new();
    
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

    public async Task Stop() => await _cts.CancelAsync();

    public void Dispose()
    {
        client.Dispose();
        _cts.Dispose();
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
            
           /* await using AudioOutStream? player = client.CreatePCMStream(AudioApplication.Music);
            
            Command ffmpeg = Cli.Wrap("ffmpeg")
                .WithArguments([
                    "-hide_banner",
                    "-loglevel", "panic",
                    "-i", track.StreamUrl,
                    "-ac", "2",
                    "-f", "s16le",
                    "-ar", "48000",
                    "pipe:1"
                ])
                .WithStandardOutputPipe(PipeTarget.ToStream(player))
                .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.Error.WriteLine));*/
            
            var x = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{track.StreamUrl}\" -ac 2 -f s16le -ar 48000 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
            })!;
            using (var y = x)
            using (var output = y.StandardOutput.BaseStream)
            using (var discord = client.CreatePCMStream(AudioApplication.Mixed))
            {
                try { await output.CopyToAsync(discord); }
                finally { await discord.FlushAsync(); }
            }
            
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, _currentTrackToken.Token);
            CancellationToken linkedToken = linkedCts.Token;

            try
            {
                //await ffmpeg.ExecuteAsync(linkedToken, linkedToken);
            }
            catch (OperationCanceledException) when (_currentTrackToken.IsCancellationRequested)
            {
                //await player.FlushAsync(_cts.Token);
            }
            catch
            {
                _queue.Clear();
                //player.Flush();
                
                _cts.Dispose();
                _cts = new CancellationTokenSource();
            }
            finally
            {
                _currentTrackToken.Dispose();
                _currentTrackToken = null;
            }
        }

        _isPlaying = false;
    }
}