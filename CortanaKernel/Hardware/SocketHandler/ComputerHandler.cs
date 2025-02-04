using System.Net.Sockets;
using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.SocketHandler;

public class ComputerHandler : ClientHandler
{
	private static readonly Lock InstanceLock = new();
	private static ComputerHandler? _instance;
	private readonly Stack<string> _messages = [];

	public ComputerHandler (Socket socket) : base(socket, "Computer")
	{
		UpdateComputerStatus(EPowerStatus.On);
		Service.ResetControllerTimer();
	}

	protected override void HandleRead(string message)
	{
		switch (message)
		{
			case "SYN":
				UpdateComputerStatus(EPowerStatus.On);
				break;
			default:
				Monitor.Enter(_messages);
				_messages.Push(message);
				Monitor.Pulse(_messages);
				Monitor.Exit(_messages);
				break;
		}
	}
	
	protected override void DisconnectSocket()
	{
		base.DisconnectSocket();
		_messages.Clear();
		_instance = null;
		UpdateComputerStatus(EPowerStatus.Off);
		Service.ResetControllerTimer();
	}
	
	// Static methods

	public static void Boot()
	{
		Helper.RunCommand(RaspberryHandler.DecodeCommand("wakeonlan", Service.NetworkData.DesktopMac));
		Helper.RunCommand(RaspberryHandler.DecodeCommand("etherwake", Service.NetworkData.DesktopMac));
	}

	public static bool Shutdown()
	{
		return _instance?.Write("shutdown") ?? false;
	}
	
	public static bool Suspend()
	{
		return _instance?.Write("suspend") ?? false;
	}
	
	public static bool Reboot()
	{
		return _instance?.Write("reboot") ?? false;
	}

	public static bool SwitchOs()
	{
		return _instance?.Write("system") ?? false;
	}
	
	public static bool Notify(string text)
	{
		return (_instance?.Write("notify") ?? false) && _instance.Write(text);
	}
	
	public static bool Command(string cmd)
	{
		return (_instance?.Write("cmd") ?? false) && _instance.Write(cmd);
	}

	public static bool GatherMessage(out string? message)
	{
		message = null;
		if(_instance == null) return false;
		
		Stack<string> instanceMessage = _instance._messages;
		Monitor.Enter(instanceMessage);
		try
		{
			if(Monitor.Wait(instanceMessage, 4000)) message = instanceMessage.Pop();
		}
		finally
		{
			Monitor.Exit(instanceMessage);
		}
		return message != null;
	}
	
	public static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(Service.NetworkData.DesktopIp) || GetComputerStatus() == EPowerStatus.On) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(15000);
		else await Task.Delay(5000);
	}
	
	private static void UpdateComputerStatus(EPowerStatus power)
	{
		DeviceHandler.DeviceStatus[EDevice.Computer] = power;
	}
	
	private static EPowerStatus GetComputerStatus()
	{
		return DeviceHandler.DeviceStatus[EDevice.Computer];
	}
	
	public static void BindNew(ComputerHandler computerHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = computerHandler;
		}
	}
    
	public static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = null;
		}
	}
}