using System.Net.Sockets;
using System.Threading.Channels;
using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.SocketHandler;

public class ComputerHandler : ClientHandler
{
	private static readonly Lock InstanceLock = new();
	private static ComputerHandler? _instance;

	private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(8);

		private readonly Channel<string> _messages = Channel.CreateBounded<string>(
		new BoundedChannelOptions(32) { FullMode = BoundedChannelFullMode.DropOldest });

	public ComputerHandler(Socket socket, string? pendingData = null) : base(socket, "Computer", pendingData)
	{
		UpdateComputerStatus(EStatus.On);
		AutomationService.ComputerStatusUpdated();
	}

	private readonly System.Text.StringBuilder _receiveBuffer = new();

	protected override void HandleRead(string chunk)
	{
		lock (_receiveBuffer)
		{
			_receiveBuffer.Append(chunk);
			string buffer = _receiveBuffer.ToString();

			int newline;
			var consumed = 0;
			while ((newline = buffer.IndexOf('\n', consumed)) >= 0)
			{
				string frame = buffer[consumed..newline].Trim('\r');
				consumed = newline + 1;
				if (frame.Length > 0) Dispatch(frame);
			}

			_receiveBuffer.Remove(0, consumed);
			if (_receiveBuffer.Length > 65536) _receiveBuffer.Clear();
		}
	}

	private void Dispatch(string message)
	{
		if (message == "SYN")
		{
			UpdateComputerStatus(EStatus.On);
			return;
		}
		_messages.Writer.TryWrite(message);
	}

	private bool Send(string message) => Write(message + "\n");

	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();
		_messages.Writer.TryComplete();

		lock (InstanceLock)
		{
			if (ReferenceEquals(_instance, this)) _instance = null;
		}

		UpdateComputerStatus(EStatus.Off);
		AutomationService.ComputerStatusUpdated();
	}

	private async Task<string?> AwaitReply()
	{
		using var cts = new CancellationTokenSource(ReplyTimeout);
		try
		{
			return await _messages.Reader.ReadAsync(cts.Token);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static ComputerHandler? Instance
	{
		get { lock (InstanceLock) return _instance; }
	}

	public static void Boot()
	{
		Helper.RunCommand(RaspberryHandler.DecodeCommand("wakeonlan", AutomationService.NetworkData.DesktopMac));
		Helper.RunCommand(RaspberryHandler.DecodeCommand("etherwake", AutomationService.NetworkData.DesktopMac));
	}

	public static bool Shutdown() => Instance?.Send("shutdown") ?? false;
	public static bool Suspend() => Instance?.Send("suspend") ?? false;
	public static bool Reboot() => Instance?.Send("reboot") ?? false;
	public static bool SwitchOs() => Instance?.Send("system") ?? false;

	public static bool Notify(string text)
	{
		ComputerHandler? instance = Instance;
		return instance != null && instance.Send("notify") && instance.Send(text);
	}

		public static async Task<StringResult> RunCommand(string cmd)
	{
		ComputerHandler? instance = Instance;
		if (instance == null) return StringResult.Failure("Computer is not connected");
		if (!instance.Send("cmd") || !instance.Send(cmd)) return StringResult.Failure("Could not reach the computer");

		string? reply = await instance.AwaitReply();
		return StringResult.Success(reply ?? "Command executed");
	}

		public static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(AutomationService.NetworkData.DesktopIp) || GetComputerStatus() == EStatus.On) && (DateTime.Now - start).TotalSeconds <= 100)
			await Task.Delay(1500);

		await Task.Delay((DateTime.Now - start).TotalSeconds < 3 ? 15000 : 5000);
	}

	private static void UpdateComputerStatus(EStatus power)
	{
		EStatus previous = DeviceHandler.DeviceStatus[EDevice.Computer];
		DeviceHandler.DeviceStatus[EDevice.Computer] = power;
		if (power == EStatus.On) DeviceHandler.DeviceStatus[EDevice.Power] = EStatus.On;
		SystemEvents.Notify();

		if (previous != power) ScheduleService.RaiseEvent(power == EStatus.On ? EScheduleEvent.ComputerOn : EScheduleEvent.ComputerOff);
	}

	private static EStatus GetComputerStatus() => DeviceHandler.DeviceStatus[EDevice.Computer];

	public static void BindNew(ComputerHandler computerHandler)
	{
		ComputerHandler? previous;
		lock (InstanceLock)
		{
			previous = _instance;
			_instance = computerHandler;
		}
		previous?.DisconnectIfAvailable();
	}

	public static void Interrupt()
	{
		ComputerHandler? previous;
		lock (InstanceLock)
		{
			previous = _instance;
			_instance = null;
		}
		previous?.DisconnectIfAvailable();
	}
}
