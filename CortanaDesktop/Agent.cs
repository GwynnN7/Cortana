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

			Send(JsonSerializer.Serialize(Hello(), CortanaEnvironment.WireJson));
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

	private static SourceHello Hello() => new(
		Wire.Hello, Wire.Magic, Wire.Version, SourceIds.Computer, SourceKind.Computer,
		[DeviceIds.Computer],
		["cpu", "cpu_temp", "gpu", "gpu_temp", "gpu_power", "ram", "disk", "at_desk", "locked"],
		Facts(MachineMetrics.Collect()));

	/// The slow things that describe this machine rather than measure it
	private static Dictionary<string, string> Facts(MachineSample sample) => new()
	{
		["name"] = sample.Host,
		["os"] = sample.Os,
		["memory"] = $"{sample.MemoryUsed:F1}/{sample.MemoryTotal:F1} GB",
		["disk"] = $"{sample.DiskUsed:F0}/{sample.DiskTotal:F0} GB",
		["uptime"] = Units.Elapsed(TimeSpan.FromSeconds(sample.Uptime))
	};

	private static Dictionary<string, double> Readings(MachineSample sample) => new()
	{
		["cpu"] = Math.Round(sample.CpuLoad, 1),
		["cpu_temp"] = Math.Round(sample.CpuTemp, 1),
		["gpu"] = Math.Round(sample.GpuLoad, 1),
		["gpu_temp"] = Math.Round(sample.GpuTemp, 1),
		["gpu_power"] = Math.Round(sample.GpuPower, 1),
		["ram"] = sample.MemoryTotal > 0 ? Math.Round(sample.MemoryUsed / sample.MemoryTotal * 100, 1) : 0,
		["disk"] = sample.DiskTotal > 0 ? Math.Round(sample.DiskUsed / sample.DiskTotal * 100, 1) : 0
	};

	private static void Report(DesktopActivity activity)
	{
		Send(JsonSerializer.Serialize(new AgentMessage("activity", Activity: activity), CortanaEnvironment.WireJson));

		Send(JsonSerializer.Serialize(new SourceReading(Wire.Reading, new Dictionary<string, double>
		{
			["at_desk"] = activity is { Locked: false, IdleSeconds: 0 } ? 1 : 0,
			["locked"] = activity.Locked ? 1 : 0
		}), CortanaEnvironment.WireJson));
	}

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

		while (true)
		{
			await Task.Delay(MetricsInterval);

			try
			{
				MachineSample sample = MachineMetrics.Collect();

				Send(JsonSerializer.Serialize(new SourceReading(Wire.Reading, Readings(sample)), CortanaEnvironment.WireJson));
				Send(JsonSerializer.Serialize(new SourceDescription(Wire.Facts, Facts(sample)), CortanaEnvironment.WireJson));
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
