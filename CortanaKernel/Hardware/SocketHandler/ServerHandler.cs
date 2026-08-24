using System.Net;
using System.Net.Sockets;
using System.Text;
using CortanaLib;

namespace CortanaKernel.Hardware.SocketHandler;

public static class ServerHandler
{
	private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(10);
	private static Socket? _server;

	public static void Initialize()
	{
		_server = new Socket(SocketType.Stream, ProtocolType.Tcp);

		_server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		_server.Bind(new IPEndPoint(IPAddress.Any, int.Parse(DataHandler.Env("CORTANA_TCP_PORT"))));
	}

	public static async Task StartListening()
	{
		Socket? server = _server;
		if (server == null) return;

		server.Listen(8);

		while (true)
		{
			Socket socket;
			try
			{
				socket = await server.AcceptAsync();
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Server] Stopped accepting connections: {ex.Message}");
				return;
			}

			_ = Task.Run(() => HandleConnection(socket));
		}
	}

	private static async Task HandleConnection(Socket socket)
	{
		try
		{
			byte[] buffer = new byte[1024];
			using var cts = new CancellationTokenSource(HandshakeTimeout);

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
				DataHandler.Log($"[Server] Rejected unknown client handshake: '{Truncate(message)}'");
				await socket.SendAsync(Encoding.UTF8.GetBytes("FIN\n"), SocketFlags.None);
				socket.Close();
				return;
			}

			string pending = message[identity.Length..];
			await socket.SendAsync(Encoding.UTF8.GetBytes("ACK\n"), SocketFlags.None);

			switch (identity)
			{
				case "computer":
					ComputerHandler.BindNew(new ComputerHandler(socket, pending));
					break;
				case "esp32":
					SensorsHandler.BindNew(new SensorsHandler(socket, pending));
					break;
			}
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Server] Handshake failed: {ex.Message}");
			try { socket.Close(); } catch {  }
		}
	}

	private static string Truncate(string value) => value.Length <= 40 ? value : string.Concat(value.AsSpan(0, 40), "...");

	public static void ShutdownServer()
	{
		_server?.Close();
		_server = null;
	}
}
