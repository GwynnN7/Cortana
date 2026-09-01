using System.Net;
using System.Net.Sockets;
using System.Text;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Network;

/// One TCP port for both machines that talk to the Kernel. The first bytes identify the client and the connection is handed to the matching handler
public sealed class ConnectionServer(DesktopComputerEndpoint desktop, Esp32SensorSource station) : BackgroundService
{
	private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);

	private Socket? _server;

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		int port = int.Parse(CortanaEnvironment.Require("CORTANA_TCP_PORT"));

		_server = new Socket(SocketType.Stream, ProtocolType.Tcp);
		_server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_server.Bind(new IPEndPoint(IPAddress.Any, port));
		_server.Listen(8);

		Log.Write("Network", $"Listening for the desktop and the station on port {port}");

		while (!stoppingToken.IsCancellationRequested)
		{
			Socket socket;
			try
			{
				socket = await _server.AcceptAsync(stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				Log.Error("Network", $"Stopped accepting connections: {ex.Message}");
				return;
			}

			_ = Task.Run(() => Handshake(socket, stoppingToken), stoppingToken);
		}
	}

	private async Task Handshake(Socket socket, CancellationToken token)
	{
		try
		{
			byte[] buffer = new byte[1024];
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
			cts.CancelAfter(HandshakeTimeout);

			int received = await socket.ReceiveAsync(buffer, SocketFlags.None, cts.Token);
			if (received == 0)
			{
				socket.Close();
				return;
			}

			string message = Encoding.UTF8.GetString(buffer, 0, received);
			string? identity = new[] { "computer", "esp32" }.FirstOrDefault(name => message.StartsWith(name, StringComparison.OrdinalIgnoreCase));

			if (identity == null)
			{
				Log.Write("Network", $"Rejected an unknown client: '{Truncate(message)}'");
				await socket.SendAsync(Encoding.UTF8.GetBytes("FIN\n"), SocketFlags.None, token);
				socket.Close();
				return;
			}

			string pending = message[identity.Length..];
			await socket.SendAsync(Encoding.UTF8.GetBytes("ACK\n"), SocketFlags.None, token);

			if (identity == "computer") desktop.Bind(socket, pending);
			else station.Bind(socket, pending);
		}
		catch (Exception ex)
		{
			Log.Error("Network", $"Handshake failed: {ex.Message}");
			try { socket.Close(); } catch { /* already gone */ }
		}
	}

	private static string Truncate(string value) => value.Length <= 40 ? value : string.Concat(value.AsSpan(0, 40), "...");

	public override void Dispose()
	{
		_server?.Close();
		_server = null;
		base.Dispose();
	}
}

/// Shared plumbing for a long-lived client socket
public abstract class SocketClient
{
	private static readonly TimeSpan DropAfter = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);

	private readonly Lock _gate = new();
	private readonly CancellationTokenSource _lifetime = new();
	private readonly string _name;

	private Socket? _socket;
	private DateTime _lastSeen = DateTime.UtcNow;

	protected SocketClient(Socket socket, string name, string? pending)
	{
		_name = name;
		_socket = socket;
		_socket.SendTimeout = 2000;

		if (!string.IsNullOrEmpty(pending)) SafeRead(pending);

		_ = Task.Run(ReadLoop);
		_ = Task.Run(WatchdogLoop);
	}

	public bool Alive
	{
		get { lock (_gate) return _socket != null; }
	}

	protected abstract void OnData(string chunk);

	protected virtual void OnClosed() { }

	protected bool Send(string message)
	{
		try
		{
			Socket? socket;
			lock (_gate) socket = _socket;
			if (socket == null) return false;

			socket.Send(Encoding.UTF8.GetBytes(message));
			return true;
		}
		catch (Exception ex)
		{
			Log.Error(_name, $"Write failed: {ex.Message}");
			Close();
			return false;
		}
	}

	public void Close()
	{
		lock (_gate)
		{
			if (_socket == null) return;

			try { _socket.Shutdown(SocketShutdown.Both); } catch { /* already gone */ }
			_socket.Close();
			_socket = null;
		}

		try { _lifetime.Cancel(); } catch (ObjectDisposedException) { }
		OnClosed();
	}

	private async Task ReadLoop()
	{
		byte[] buffer = new byte[4096];

		try
		{
			while (!_lifetime.IsCancellationRequested)
			{
				Socket? socket;
				lock (_gate) socket = _socket;
				if (socket == null) break;

				int received = await socket.ReceiveAsync(buffer, SocketFlags.None, _lifetime.Token);
				if (received == 0) break;

				_lastSeen = DateTime.UtcNow;
				SafeRead(Encoding.UTF8.GetString(buffer, 0, received));
			}
		}
		catch (OperationCanceledException) { }
		catch (Exception ex)
		{
			Log.Write(_name, $"The read loop ended: {ex.Message}");
		}
		finally
		{
			Close();
		}
	}

	private async Task WatchdogLoop()
	{
		try
		{
			while (!_lifetime.IsCancellationRequested)
			{
				await Task.Delay(WatchdogInterval, _lifetime.Token);
				if (DateTime.UtcNow - _lastSeen <= DropAfter) continue;

				Log.Write(_name, $"Silent for {DropAfter.TotalSeconds:0}s, dropping the connection");
				Close();
				return;
			}
		}
		catch (OperationCanceledException) { }
	}

	private void SafeRead(string chunk)
	{
		try
		{
			OnData(chunk);
		}
		catch (Exception ex)
		{
			Log.Error(_name, $"Could not handle a message: {ex.Message}");
		}
	}
}
