using System.Net;
using System.Net.Sockets;
using System.Text;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaDesktop;

public static class CortanaDesktop
{
	private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(4);
	private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(2);

	internal static DesktopInfo DesktopInfo { get; private set; }

	private static readonly Lock SocketLock = new();
	private static Socket? _computerSocket;

	private static async Task Main()
	{
		DesktopInfo = GetClientInfo();

		string address = await ResolveCortanaAddress();
		_ = Task.Run(KeepAliveLoop);

		while (true)
		{
			if (CreateSocketConnection(address, DesktopInfo.TcpPort))
			{
				Write("computer");
				await ReadLoop();
			}

			await Task.Delay(ReconnectDelay);
		}
	}

		private static async Task<string> ResolveCortanaAddress()
	{
		while (true)
		{
			IOption<SensorResponse> gatewayOption = await GetCortanaGateway();

			string address = gatewayOption.Match(
				gateway => gateway.Value[..(gateway.Value.LastIndexOf('.') + 1)] + DesktopInfo.NetworkAddr,
				() => "");

			if (!string.IsNullOrEmpty(address)) return address;

			DataHandler.Log("Cortana not reachable, can't find correct address");
			await Task.Delay(3000);
		}
	}

	private static async Task<IOption<SensorResponse>> GetCortanaGateway()
	{
		try
		{
			return await ApiHandler.Get<SensorResponse>($"{ERoute.Raspberry}/{ERaspberryInfo.Gateway}");
		}
		catch
		{
			return new None<SensorResponse>();
		}
	}

	private static bool CreateSocketConnection(string address, int port)
	{
		try
		{
			DisconnectClient();

			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { SendTimeout = 2000 };
			socket.Connect(new IPEndPoint(IPAddress.Parse(address), port));

			lock (SocketLock) _computerSocket = socket;

			OsHandler.ExecuteCommand("notify", "Cortana connected", false);
			return true;
		}
		catch (Exception ex)
		{
			DataHandler.Log($"Could not connect to Cortana: {ex.Message}");
			DisconnectClient();
			return false;
		}
	}

		private static async Task KeepAliveLoop()
	{
		while (true)
		{
			await Task.Delay(KeepAliveInterval);
			Write("SYN");
		}
	}

	private static async Task ReadLoop()
	{
		Socket? socket;
		lock (SocketLock) socket = _computerSocket;
		if (socket == null) return;

		string? textCommand = null;
		byte[] buffer = new byte[4096];
		var pending = new StringBuilder();

		try
		{
			while (true)
			{
				int received = await socket.ReceiveAsync(buffer, SocketFlags.None);
				if (received == 0) break;

				pending.Append(Encoding.UTF8.GetString(buffer, 0, received));
				string stream = pending.ToString();

				int newline;
				var consumed = 0;
				while ((newline = stream.IndexOf('\n', consumed)) >= 0)
				{
					string message = stream[consumed..newline].Trim('\r');
					consumed = newline + 1;
					if (message.Length == 0) continue;

					switch (message)
					{
						case "shutdown" or "suspend" or "reboot" or "system":
							OsHandler.ExecuteCommand(message);
							break;
						case "notify" or "cmd":
							textCommand = message;
							break;
						default:
							if (textCommand != null) OsHandler.ExecuteCommand(textCommand, message);
							textCommand = null;
							break;
					}
				}

				pending.Remove(0, consumed);
				if (pending.Length > 65536) pending.Clear();
			}
		}
		catch (Exception ex)
		{
			DataHandler.Log($"Connection lost: {ex.Message}");
		}
		finally
		{
			DisconnectClient();
			OsHandler.ExecuteCommand("notify", "Cortana disconnected", false);
		}
	}

	internal static void Write(string message)
	{
		Socket? socket;
		lock (SocketLock) socket = _computerSocket;
		if (socket == null) return;

		try
		{
			socket.Send(Encoding.UTF8.GetBytes(message + "\n"));
		}
		catch
		{
			DisconnectClient();
		}
	}

	private static void DisconnectClient()
	{
		lock (SocketLock)
		{
			if (_computerSocket == null) return;
			try { _computerSocket.Close(); } catch {  }
			_computerSocket = null;
		}
	}

	private static DesktopInfo GetClientInfo()
	{
		string confPath = DataHandler.CortanaPath(EDirType.Config, "Settings.json");
		if (!File.Exists(confPath)) throw new CortanaException("Unknown client connection info");
		return DataHandler.DeserializeJson<DesktopInfo>(confPath);
	}
}
