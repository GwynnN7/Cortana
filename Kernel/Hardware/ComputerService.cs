using System.Net.Sockets;
using System.Text;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware;

internal static class ComputerService
{
	private static Socket? _computerSocket;
	private static Timer? _connectionTimer;

	static ComputerService()
	{
		RestartConnectionTimer();
	}
	
	internal static void BindSocket(Socket socket)
	{
		_computerSocket?.Close();
		
		_computerSocket = socket;
		_computerSocket.SendTimeout = 2000;
		Task.Run(Read);
		
		UpdateComputerStatus(EPower.On);
	}

	private static void Read()
	{
		try
		{
			while (true)
			{
				var buffer = new byte[1024];
				int received = _computerSocket!.Receive(buffer);
				string message = Encoding.UTF8.GetString(buffer, 0, received);
				if (received == 0) continue;

				switch (message)
				{
					case "SYN":
						UpdateComputerStatus(EPower.On);
						RestartConnectionTimer();
						break;
					default:
						Software.FileHandler.Log("ComputerService", message);
						break;
				}
			}
		}
		catch (Exception ex)
		{
			Software.FileHandler.Log("ComputerService", $"Read Interrupted with error: {ex.Message}");
			DisconnectSocket();
		}
	}

	private static bool Write(string message)
	{
		try
		{
			_computerSocket!.Send(Encoding.UTF8.GetBytes(message));
			return true;
		}
		catch (Exception ex)
		{
			Software.FileHandler.Log("ComputerService", $"Write of message \"{message}\" failed with error: {ex.Message}");
			DisconnectSocket();
			return false;
		}
	}
	
	internal static void Boot()
	{
		Helper.RunScript("wake-on-lan", NetworkAdapter.ComputerMac);
	}

	internal static void Shutdown()
	{
		Write("shutdown");
	}
	
	internal static bool Reboot()
	{
		return Write("reboot");
	}
	
	internal static bool Notify(string text)
	{
		bool ready = Write("notify");
		return ready && Write(text);
	}
	
	internal static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(NetworkAdapter.ComputerIp) || GetComputerStatus() == EPower.On) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(20000);
		else await Task.Delay(5000);
	}
	
	internal static void DisconnectSocket()
	{
		_computerSocket?.Close();
		_computerSocket = null;
		UpdateComputerStatus(EPower.Off);
	}
	
	private static void ResetConnection(object? sender, EventArgs e) 
	{
		DisconnectSocket();
		RestartConnectionTimer();
	}

	private static void RestartConnectionTimer()
	{
		_connectionTimer?.Stop();
		_connectionTimer?.Close();
		_connectionTimer = new Timer("connection-timer", null, (10, 0, 0), ResetConnection, ETimerType.Utility, ETimerLoop.No);
	}
	
	private static void UpdateComputerStatus(EPower power)
	{
		DeviceHandler.HardwareStates[EDevice.Computer] = power;
	}
	
	private static EPower GetComputerStatus()
	{
		return DeviceHandler.HardwareStates[EDevice.Computer];
	}
}