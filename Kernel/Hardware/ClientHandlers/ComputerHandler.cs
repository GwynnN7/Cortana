using System.Net.Sockets;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware.ClientHandlers;

internal class ComputerHandler : ClientHandler
{
	private static readonly Lock InstanceLock = new();
	private static ComputerHandler? _instance;
	private readonly Stack<string> _messages = [];

	internal ComputerHandler (Socket socket) : base(socket, "Computer")
	{
		UpdateComputerStatus(EPower.On);
		HardwareProxy.StartNightModeTimer();
	}

	protected override void HandleRead(string message)
	{
		switch (message)
		{
			case "SYN":
				UpdateComputerStatus(EPower.On);
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
		UpdateComputerStatus(EPower.Off);
		HardwareProxy.StartNightModeTimer();
	}
	
	// Static methods

	internal static void Boot()
	{
		Helper.RunCommand(RaspberryHandler.DecodeCommand("wakeonlan", HardwareSettings.NetworkData.DesktopMac));
		Helper.RunCommand(RaspberryHandler.DecodeCommand("etherwake", HardwareSettings.NetworkData.DesktopMac));
	}

	internal static bool Shutdown()
	{
		return _instance?.Write("shutdown") ?? false;
	}
	
	internal static bool Suspend()
	{
		return _instance?.Write("suspend") ?? false;
	}
	
	internal static bool Reboot()
	{
		return _instance?.Write("reboot") ?? false;
	}

	internal static bool SwapOs()
	{
		return _instance?.Write("swap-os") ?? false;
	}
	
	internal static bool Notify(string text)
	{
		return (_instance?.Write("notify") ?? false) && _instance.Write(text);
	}
	
	internal static bool Command(string cmd)
	{
		return (_instance?.Write("cmd") ?? false) && _instance.Write(cmd);
	}

	internal static bool GatherMessage(out string? message)
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
	
	internal static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(HardwareSettings.NetworkData.DesktopIp) || GetComputerStatus() == EPower.On) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(15000);
		else await Task.Delay(5000);
	}
	
	private static void UpdateComputerStatus(EPower power)
	{
		DeviceHandler.HardwareStates[EDevice.Computer] = power;
	}
	
	private static EPower GetComputerStatus()
	{
		return DeviceHandler.HardwareStates[EDevice.Computer];
	}
	
	internal static void BindNew(ComputerHandler computerHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = computerHandler;
		}
	}
    
	internal static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectIfAvailable();
			_instance = null;
		}
	}
}