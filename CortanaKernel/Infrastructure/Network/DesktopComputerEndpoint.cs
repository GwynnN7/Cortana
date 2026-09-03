using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CortanaKernel.Application;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Infrastructure.Raspberry;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Network;

/// Wire format between the Kernel and the desktop agent
public sealed record AgentCommand(string Id, string Command, string Argument);

public sealed record AgentMessage(string Type, string Id = "", string Text = "", DesktopActivity? Activity = null,
	IReadOnlyDictionary<string, double>? Values = null, IReadOnlyDictionary<string, string>? Facts = null);

public sealed class DesktopComputerEndpoint(IComputerPresence presence, RaspberryHost host) : IComputerEndpoint
{
	private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(25);
	private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
	private static readonly TimeSpan MaxShutdownWait = TimeSpan.FromSeconds(100);

	private readonly Lock _gate = new();
	private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> _pending = new();

	private AgentConnection? _connection;

	public bool Connected
	{
		get { lock (_gate) return _connection is { Alive: true }; }
	}

	public void Bind(Socket socket, string pending)
	{
		AgentConnection? previous;
		var connection = new AgentConnection(socket, pending, Dispatch, Dropped);

		lock (_gate)
		{
			previous = _connection;
			_connection = connection;
		}

		previous?.Close();
		presence.Changed(true);
	}

	public void Disconnect()
	{
		AgentConnection? previous;
		lock (_gate)
		{
			previous = _connection;
			_connection = null;
		}

		previous?.Close();
	}

	public Result<string> WakeOnLan() => host.WakeComputer(host.Profile.DesktopMac);

	public async Task<Result<string>> Send(ComputerCommand command, string argument, CancellationToken token = default)
	{
		AgentConnection? connection;
		lock (_gate) connection = _connection;

		if (connection is not { Alive: true }) return Result.Fail<string>("The computer is not connected");

		var id = Guid.NewGuid().ToString("N")[..8];
		var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		_pending[id] = completion;

		try
		{
			if (!connection.Send(new AgentCommand(id, command.ToString(), argument)))
				return Result.Fail<string>("Could not reach the computer");

			// Fire-and-forget commands do not keep the caller waiting for powering down
			if (command is ComputerCommand.Shutdown or ComputerCommand.Suspend or ComputerCommand.Reboot
				or ComputerCommand.BootIntoOtherOperatingSystem or ComputerCommand.Notify)
				return Result.Ok($"{command} sent to the computer");

			using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
			cts.CancelAfter(ReplyTimeout);
			await using CancellationTokenRegistration registration = cts.Token.Register(() => completion.TrySetCanceled());

			return Result.Ok(await completion.Task);
		}
		catch (OperationCanceledException)
		{
			return Result.Fail<string>("The computer did not answer, it may have just disconnected");
		}
		finally
		{
			_pending.TryRemove(id, out _);
		}
	}

	/// The agent process dies before powering down, so also ping the address and then wait out a configured time
	public async Task WaitUntilPoweredOff(TimeSpan grace, CancellationToken token = default)
	{
		DateTimeOffset start = DateTimeOffset.Now;
		string address = host.Profile.DesktopIp;

		await Task.Delay(TimeSpan.FromSeconds(1), token);

		while (DateTimeOffset.Now - start < MaxShutdownWait)
		{
			if (!Connected && !RaspberryHost.Ping(address)) break;
			await Task.Delay(PollInterval, token);
		}

		await Task.Delay(grace, token);
	}

	private void Dispatch(AgentMessage message)
	{
		switch (message.Type)
		{
			case "reply" when _pending.TryRemove(message.Id, out TaskCompletionSource<string>? completion):
				completion.TrySetResult(message.Text);
				break;
			case "activity" when message.Activity is not null:
				presence.ActivityChanged(message.Activity);
				break;
			case Wire.Reading when message.Values is not null:
				presence.Observed(message.Values);
				break;
			case Wire.Facts when message.Facts is not null:
				presence.Described(message.Facts);
				break;
		}
	}

	private void Dropped(AgentConnection connection)
	{
		bool wasCurrent;
		lock (_gate)
		{
			wasCurrent = ReferenceEquals(_connection, connection);
			if (wasCurrent) _connection = null;
		}

		foreach (TaskCompletionSource<string> completion in _pending.Values) completion.TrySetCanceled();
		_pending.Clear();

		if (wasCurrent) presence.Changed(false);
	}

	private sealed class AgentConnection(Socket socket, string pending, Action<AgentMessage> dispatch, Action<AgentConnection> dropped)
		: SocketClient(socket, "Computer", pending)
	{
		private readonly StringBuilder _buffer = new();

		public bool Send(AgentCommand command) => Send(JsonSerializer.Serialize(command, CortanaEnvironment.WireJson) + "\n");

		protected override void OnData(string chunk)
		{
			lock (_buffer)
			{
				_buffer.Append(chunk);
				string text = _buffer.ToString();

				int newline;
				var consumed = 0;
				while ((newline = text.IndexOf('\n', consumed)) >= 0)
				{
					string line = text[consumed..newline].Trim('\r', ' ');
					consumed = newline + 1;
					if (line.Length > 0) Handle(line);
				}

				_buffer.Remove(0, consumed);
				if (_buffer.Length > 65536) _buffer.Clear();
			}
		}

		private void Handle(string line)
		{
			// Anything that is not a JSON object doesn't count
			if (!line.StartsWith('{')) return;

			try
			{
				AgentMessage? message = JsonSerializer.Deserialize<AgentMessage>(line, CortanaEnvironment.WireJson);
				if (message != null && message.Type != "ping") dispatch(message);
			}
			catch (JsonException ex)
			{
				Log.Write("Computer", $"Dropping a malformed message: {ex.Message}");
			}
		}

		protected override void OnClosed() => dropped(this);
	}
}
