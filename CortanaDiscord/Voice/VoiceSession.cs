using System.Collections.Concurrent;
using System.Diagnostics;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

namespace CortanaDiscord.Voice;

public sealed class VoiceSession : IAsyncDisposable
{
		private const int FrameBytes = 3840;
	private const int PcmBufferMillis = 1000;
	private const int PrimeFrames = 30;

	private static readonly TimeSpan ConnectHandshakeTimeout = TimeSpan.FromSeconds(15);
	private static readonly TimeSpan GatewayCallTimeout = TimeSpan.FromSeconds(25);
	private static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(40);

	private readonly SocketGuild _guild;
	private readonly SemaphoreSlim _connectionGate = new(1, 1);
	private readonly ConcurrentQueue<AudioTrack> _queue = new();
	private readonly SemaphoreSlim _queueSignal = new(0);
	private readonly CancellationTokenSource _lifetime = new();

	private readonly Lock _playbackLock = new();
	private CancellationTokenSource? _currentTrack;
	private Task _currentPlayback = Task.CompletedTask;

	private IAudioClient? _audioClient;
	private AudioOutStream? _pcmStream;
	private Task? _worker;
	private int _primed;
	private volatile bool _disposed;

	public VoiceSession(SocketGuild guild) => _guild = guild;

	public ulong GuildId => _guild.Id;
	public SocketVoiceChannel? CurrentChannel { get; private set; }
	public bool IsConnected => _audioClient is { ConnectionState: ConnectionState.Connected };
	public int QueueLength => _queue.Count;

		public AudioTrack? CurrentTrack { get; private set; }

	public IReadOnlyCollection<string> QueuedTitles => _queue.Select(track => track.Title).ToList();

	public async Task<string> ConnectAsync(SocketVoiceChannel channel)
	{
		if (_disposed) return "Non sono più disponibile";

		if (!await _connectionGate.WaitAsync(GateTimeout)) return "Sto ancora chiudendo la connessione precedente, riprova";
		try
		{
			if (IsConnected && CurrentChannel?.Id == channel.Id) return "Sono già qui";

			await TeardownConnectionAsync();

			DataHandler.Log($"[Voice] Connecting to '{channel.Name}' in '{_guild.Name}'");
			IAudioClient? client = await channel.ConnectAsync(selfDeaf: true, selfMute: false).WaitAsync(GatewayCallTimeout);
			if (client == null) return "Non riesco a connettermi al canale vocale";

			if (!await WaitForHandshake(client))
			{
				DataHandler.Log($"[Voice] Handshake to '{channel.Name}' timed out (state: {client.ConnectionState})");
				await SafeStop(client);
				return "La connessione al canale vocale non si è completata";
			}

			client.Disconnected += OnClientDisconnected;

			_audioClient = client;
			
			_pcmStream = client.CreatePCMStream(AudioApplication.Mixed, bitrate: null, bufferMillis: PcmBufferMillis);
			CurrentChannel = channel;

			DataHandler.Log($"[Voice] Connected to '{channel.Name}'");
			EnsureWorker();
			Enqueue(HelloTrack());
			return "Arrivo";
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Voice] Connect failed: {ex.Message}");
			return "Non riesco a connettermi al canale vocale";
		}
		finally
		{
			_connectionGate.Release();
		}
	}

		private static async Task<bool> WaitForHandshake(IAudioClient client)
	{
		if (client.ConnectionState == ConnectionState.Connected) return true;

		var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		Task OnConnected()
		{
			ready.TrySetResult();
			return Task.CompletedTask;
		}

		client.Connected += OnConnected;
		try
		{
			if (client.ConnectionState == ConnectionState.Connected) return true;

			Task completed = await Task.WhenAny(ready.Task, Task.Delay(ConnectHandshakeTimeout));
			return completed == ready.Task || client.ConnectionState == ConnectionState.Connected;
		}
		finally
		{
			client.Connected -= OnConnected;
		}
	}

	private Task OnClientDisconnected(Exception exception)
	{
		DataHandler.Log($"[Voice] Voice client dropped in '{_guild.Name}': {exception.Message}");
		_pcmStream = null;
		CurrentChannel = null;
		return Task.CompletedTask;
	}

	public async Task<string> DisconnectAsync()
	{
		if (!await _connectionGate.WaitAsync(GateTimeout)) return "Sto ancora gestendo la connessione precedente, riprova";
		try
		{
			if (_audioClient == null && CurrentChannel == null) return "Non sono connessa a nessun canale";
			await TeardownConnectionAsync();
			return "Mi sto disconnettendo";
		}
		finally
		{
			_connectionGate.Release();
		}
	}

	private async Task TeardownConnectionAsync()
	{
		Clear();
		await SkipAsync();
		
		await WaitForPlaybackToStop();

		Interlocked.Exchange(ref _primed, 0);

		AudioOutStream? stream = _pcmStream;
		_pcmStream = null;
		if (stream != null)
		{
			try { await stream.FlushAsync(); } catch {  }
			await stream.DisposeAsync();
		}

		IAudioClient? client = _audioClient;
		_audioClient = null;
		if (client != null)
		{
			client.Disconnected -= OnClientDisconnected;
			await SafeStop(client);
		}

		SocketVoiceChannel? channel = CurrentChannel;
		CurrentChannel = null;
		if (channel != null)
		{
			try { await channel.DisconnectAsync().WaitAsync(GatewayCallTimeout); }
			catch (Exception ex) { DataHandler.Log($"[Voice] Disconnect failed: {ex.Message}"); }
		}
	}

	private async Task WaitForPlaybackToStop()
	{
		Task playback;
		lock (_playbackLock) playback = _currentPlayback;

		try
		{
			await playback.WaitAsync(TimeSpan.FromSeconds(3));
		}
		catch (Exception)
		{
		}
	}

	private static async Task SafeStop(IAudioClient client)
	{
		try { await client.StopAsync(); } catch {  }
		try { client.Dispose(); } catch {  }
	}

	public bool Enqueue(AudioTrack track)
	{
		if (_disposed || string.IsNullOrWhiteSpace(track.StreamUrl)) return false;
		if (!IsConnected) return false;

		_queue.Enqueue(track);
		try
		{
			_queueSignal.Release();
		}
		catch (ObjectDisposedException)
		{
			return false;
		}

		EnsureWorker();
		return true;
	}

	public bool Clear()
	{
		var removed = false;
		while (_queue.TryDequeue(out _)) removed = true;
		return removed;
	}

	public Task<bool> SkipAsync()
	{
		CancellationTokenSource? token;
		lock (_playbackLock) token = _currentTrack;

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

	private void EnsureWorker()
	{
		lock (_playbackLock)
		{
			if (_worker is { IsCompleted: false }) return;
			_worker = Task.Run(ProcessQueueAsync);
		}
	}

	private async Task ProcessQueueAsync()
	{
		while (!_lifetime.IsCancellationRequested)
		{
			try
			{
				await _queueSignal.WaitAsync(_lifetime.Token);
			}
			catch (Exception)
			{
				return;
			}

			if (!_queue.TryDequeue(out AudioTrack? track)) continue;

			using var trackToken = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
			lock (_playbackLock)
			{
				if (_disposed) return;
				_currentTrack = trackToken;
			}

			try
			{
				Task playback = PlayTrackAsync(track, trackToken.Token);
				lock (_playbackLock) _currentPlayback = playback;
				await playback;
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Voice] Playback of '{track.Title}' failed: {ex.Message}");
			}
			finally
			{
				lock (_playbackLock)
				{
					if (ReferenceEquals(_currentTrack, trackToken)) _currentTrack = null;
				}
			}
		}
	}

	private async Task PlayTrackAsync(AudioTrack track, CancellationToken token)
	{
		AudioOutStream? destination = _pcmStream;
		IAudioClient? client = _audioClient;
		if (destination == null || client == null)
		{
			DataHandler.Log($"[Voice] Dropping '{track.Title}': not connected");
			return;
		}

		CurrentTrack = track;
		DataHandler.Log($"[Voice] Playing '{track.Title}'");

		if (Interlocked.CompareExchange(ref _primed, 1, 0) == 0)
		{
			byte[] silence = new byte[FrameBytes];
			for (var i = 0; i < PrimeFrames; i++) await destination.WriteAsync(silence, token);
		}
		using Process ffmpeg = StartFfmpeg(track.StreamUrl);

		Task<string> stderr = ffmpeg.StandardError.ReadToEndAsync(CancellationToken.None);

		try
		{
			await client.SetSpeakingAsync(true);
			await PumpAsync(ffmpeg.StandardOutput.BaseStream, destination, token);
			
			if (!token.IsCancellationRequested) await destination.FlushAsync(CancellationToken.None);
		}
		finally
		{
			try { await client.SetSpeakingAsync(false); } catch {  }

			if (!ffmpeg.HasExited)
			{
				try { ffmpeg.Kill(entireProcessTree: true); } catch {  }
			}

			string errors = await stderr;
			if (!string.IsNullOrWhiteSpace(errors)) DataHandler.Log($"[Voice] ffmpeg: {errors.Trim()}");

			CurrentTrack = null;
			DataHandler.Log($"[Voice] Finished '{track.Title}'");
		}
	}

		private static async Task PumpAsync(Stream source, Stream destination, CancellationToken token)
	{
		byte[] buffer = new byte[FrameBytes];

		while (!token.IsCancellationRequested)
		{
			var filled = 0;
			while (filled < buffer.Length)
			{
				int read = await source.ReadAsync(buffer.AsMemory(filled), token);
				if (read == 0) break;
				filled += read;
			}

			if (filled == 0) return;

			await destination.WriteAsync(buffer.AsMemory(0, filled), token);
			if (filled < buffer.Length) return;
		}
	}

	private static Process StartFfmpeg(string path)
	{
		var info = new ProcessStartInfo
		{
			FileName = "ffmpeg",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};

		foreach (string arg in new[]
		{
			"-hide_banner", "-loglevel", "warning", "-nostdin",
			"-reconnect", "1", "-reconnect_streamed", "1", "-reconnect_delay_max", "5",
			"-rw_timeout", "15000000",
			"-i", path,
			"-vn", "-ac", "2", "-f", "s16le", "-ar", "48000", "pipe:1"
		}) info.ArgumentList.Add(arg);

		return Process.Start(info) ?? throw new InvalidOperationException("Unable to start ffmpeg");
	}

	internal static AudioTrack HelloTrack()
	{
		string path = DataHandler.CortanaPath(EDirType.Storage, "hello.mp3");
		return new AudioTrack { Title = "Hello", OriginalUrl = path, StreamUrl = path, Duration = TimeSpan.Zero, ThumbnailUrl = "" };
	}

	public async ValueTask DisposeAsync()
	{
		if (_disposed) return;
		_disposed = true;

		try { await _lifetime.CancelAsync(); } catch (ObjectDisposedException) {  }

		Task? worker;
		lock (_playbackLock) worker = _worker;
		if (worker != null)
		{
			try { await worker.WaitAsync(TimeSpan.FromSeconds(5)); } catch {  }
		}

		await _connectionGate.WaitAsync();
		try
		{
			await TeardownConnectionAsync();
		}
		finally
		{
			_connectionGate.Release();
		}

		_connectionGate.Dispose();
		_queueSignal.Dispose();
		_lifetime.Dispose();
	}
}
