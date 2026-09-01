using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaLib.Client;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaDesktop;

internal sealed record AgentCommand(string Id, string Command, string Argument);

internal sealed record AgentMessage(string Type, string Id = "", string Text = "", DesktopActivity? Activity = null);

/// Keeps one socket to the Kernel open, answers commands and pushes this machine's metrics
internal static class Agent
{
	private static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(4);
	private static readonly TimeSpan Reconnect = TimeSpan.FromSeconds(2);
	private static readonly TimeSpan MetricsInterval = TimeSpan.FromSeconds(30);

	private static readonly Lock Gate = new();
	private static Socket? _socket;

	public static async Task<int> Run()
	{
		string host = await ResolveKernelHost();
		int port = int.Parse(CortanaEnvironment.Require("CORTANA_TCP_PORT"));

		_ = Task.Run(KeepAliveLoop);
		_ = Task.Run(MetricsLoop);
		Activity.Start(Report);

		Log.Write("Agent", $"Talking to the Kernel at {host}:{port}");

		while (true)
		{
			if (Connect(host, port)) await ReadLoop();
			await Task.Delay(Reconnect);
		}
	}

	/// One explicit host wins. Otherwise the Kernel's own gateway plus a configured last octet
	private static async Task<string> ResolveKernelHost()
	{
		if (CortanaEnvironment.Read("CORTANA_KERNEL_HOST") is { Length: > 0 } configured) return configured;

		string octet = CortanaEnvironment.Read("CORTANA_KERNEL_OCTET", "117");

		while (true)
		{
			Result<string> gateway = await CortanaClient.Default.RaspberryInfo(RaspberryInfo.Gateway);

			if (gateway.IsOk && gateway.Value.Contains('.'))
				return string.Concat(gateway.Value.AsSpan(0, gateway.Value.LastIndexOf('.') + 1), octet);

			Log.Write("Agent", "The Kernel is not reachable yet, retrying");
			await Task.Delay(TimeSpan.FromSeconds(3));
		}
	}

	private static bool Connect(string host, int port)
	{
		try
		{
			Disconnect();

			var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { SendTimeout = 2000 };
			socket.Connect(new IPEndPoint(IPAddress.Parse(host), port));

			lock (Gate) _socket = socket;

			Send("computer");
			_ = DesktopOs.Execute(ComputerCommand.Notify, "Cortana connected");
			Activity.Resend(Report);
			return true;
		}
		catch (Exception ex)
		{
			Log.Write("Agent", $"Could not connect: {ex.Message}");
			Disconnect();
			return false;
		}
	}

	private static async Task ReadLoop()
	{
		Socket? socket;
		lock (Gate) socket = _socket;
		if (socket == null) return;

		byte[] buffer = new byte[4096];
		var pending = new StringBuilder();

		try
		{
			while (true)
			{
				int received = await socket.ReceiveAsync(buffer, SocketFlags.None);
				if (received == 0) break;

				pending.Append(Encoding.UTF8.GetString(buffer, 0, received));
				string text = pending.ToString();

				int newline;
				var consumed = 0;
				while ((newline = text.IndexOf('\n', consumed)) >= 0)
				{
					string line = text[consumed..newline].Trim('\r', ' ');
					consumed = newline + 1;
					if (line.Length > 0) Handle(line);
				}

				pending.Remove(0, consumed);
				if (pending.Length > 65536) pending.Clear();
			}
		}
		catch (Exception ex)
		{
			Log.Write("Agent", $"The connection dropped: {ex.Message}");
		}
		finally
		{
			Disconnect();
			_ = DesktopOs.Execute(ComputerCommand.Notify, "Cortana disconnected");
		}
	}

	private static void Handle(string line)
	{
		// The handshake reply and anything else that is not a command are ignored
		if (!line.StartsWith('{')) return;

		AgentCommand? command;
		try
		{
			command = JsonSerializer.Deserialize<AgentCommand>(line, CortanaEnvironment.WireJson);
		}
		catch (JsonException ex)
		{
			Log.Write("Agent", $"Dropping a malformed command: {ex.Message}");
			return;
		}

		if (command == null || !Enum.TryParse(command.Command, true, out ComputerCommand parsed)) return;

		_ = Task.Run(async () =>
		{
			string result = await DesktopOs.Execute(parsed, command.Argument);
			Send(JsonSerializer.Serialize(new AgentMessage("reply", command.Id, result), CortanaEnvironment.WireJson));
		});
	}

	private static void Report(DesktopActivity activity) =>
		Send(JsonSerializer.Serialize(new AgentMessage("activity", Activity: activity), CortanaEnvironment.WireJson));

	private static async Task KeepAliveLoop()
	{
		string ping = JsonSerializer.Serialize(new AgentMessage("ping"), CortanaEnvironment.WireJson);

		while (true)
		{
			await Task.Delay(KeepAlive);
			Send(ping);
		}
	}

	private static async Task MetricsLoop()
	{
		MachineMetrics.Collect();
		CortanaClient client = CortanaClient.Default.As(CommandSurface.Desktop);

		while (true)
		{
			await Task.Delay(MetricsInterval);

			try
			{
				await client.PushMetrics(MachineMetrics.Collect());
			}
			catch (Exception ex)
			{
				Log.Write("Agent", $"Could not push metrics: {ex.Message}");
			}
		}
	}

	private static void Send(string message)
	{
		Socket? socket;
		lock (Gate) socket = _socket;
		if (socket == null) return;

		try
		{
			socket.Send(Encoding.UTF8.GetBytes(message + "\n"));
		}
		catch
		{
			Disconnect();
		}
	}

	private static void Disconnect()
	{
		lock (Gate)
		{
			if (_socket == null) return;

			try { _socket.Close(); } catch { /* already gone */ }
			_socket = null;
		}
	}
}
