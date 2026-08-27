using System.Net.Sockets;
using System.Text;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.SocketHandler;

public abstract class ClientHandler
{
	private const int SendTimeoutMs = 2000;
	private static readonly TimeSpan DisconnectAfter = TimeSpan.FromSeconds(30);
	private static readonly TimeSpan WatchdogInterval = TimeSpan.FromSeconds(5);

	private readonly Lock _socketLock = new();
	private readonly string _deviceName;
	private readonly CancellationTokenSource _lifetime = new();

	private Socket? _socket;
	private DateTime _lastSeen = DateTime.UtcNow;

	protected ClientHandler(Socket socket, string deviceName, string? pendingData = null)
	{
		_deviceName = deviceName;
		_socket = socket;
		_socket.SendTimeout = SendTimeoutMs;

		Notifier.Send(ELogSource.Sensors, $"{_deviceName} connected");

		if (!string.IsNullOrEmpty(pendingData)) SafeHandleRead(pendingData);

		_ = Task.Run(ReadLoop);
		_ = Task.Run(WatchdogLoop);
	}

	private async Task ReadLoop()
	{
		byte[] buffer = new byte[4096];
		try
		{
			while (!_lifetime.IsCancellationRequested)
			{
				Socket? socket = _socket;
				if (socket == null) break;

				int received = await socket.ReceiveAsync(buffer, SocketFlags.None, _lifetime.Token);
				
				if (received == 0) break;

				_lastSeen = DateTime.UtcNow;
				SafeHandleRead(Encoding.UTF8.GetString(buffer, 0, received));
			}
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[{_deviceName}] read loop ended: {ex.Message}");
		}
		finally
		{
			DisconnectIfAvailable();
		}
	}

		private async Task WatchdogLoop()
	{
		try
		{
			while (!_lifetime.IsCancellationRequested)
			{
				await Task.Delay(WatchdogInterval, _lifetime.Token);
				if (DateTime.UtcNow - _lastSeen <= DisconnectAfter) continue;

				DataHandler.Log($"[{_deviceName}] no data for {DisconnectAfter.TotalSeconds:0}s, dropping connection");
				DisconnectIfAvailable();
				return;
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

		private void SafeHandleRead(string message)
	{
		try
		{
			HandleRead(message);
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[{_deviceName}] failed to handle message: {ex.Message}");
		}
	}

	protected abstract void HandleRead(string message);

	protected bool Write(string message)
	{
		try
		{
			Socket? socket = _socket;
			if (socket == null) return false;
			socket.Send(Encoding.UTF8.GetBytes(message));
			return true;
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[{_deviceName}] write failed: {ex.Message}");
			DisconnectIfAvailable();
			return false;
		}
	}

	public void DisconnectIfAvailable()
	{
		lock (_socketLock)
		{
			if (_socket == null) return;
			DisconnectSocket();
		}
	}

	protected virtual void DisconnectSocket()
	{
		Notifier.Send(ELogSource.Sensors, $"{_deviceName} disconnected", ELogLevel.Warning);

		try { _socket?.Shutdown(SocketShutdown.Both); } catch {}
		_socket?.Close();
		_socket = null;

		try { _lifetime.Cancel(); } catch (ObjectDisposedException) {}
	}
}
