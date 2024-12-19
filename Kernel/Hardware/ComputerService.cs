using System.Net.Sockets;
using System.Text;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Renci.SshNet;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware;

internal static class ComputerService
{
	private static Socket? _computerClient;

	static ComputerService()
	{
		_ = new Timer("", null, (30, 0, 0), CheckConnection, ETimerType.Utility, ETimerLoop.Interval);
	}
	
	public static void BindClient(Socket handler)
	{
		_computerClient?.Close();
		_computerClient = handler;
		UpdateComputerStatus(EPower.On);
		Task.Run(Read);
	}

	private static void Read()
	{
		try
		{
			while (true)
			{
				var buffer = new byte[1_024];
				int received = _computerClient!.Receive(buffer);
				string message = Encoding.UTF8.GetString(buffer, 0, received);
				if (received == 0) continue;

				switch (message)
				{
					case "poweroff" or "reboot":
						Task.Delay(500);
						bool result = TestSocket();
						Software.FileHandler.Log("Client", $"Asked to shutdown: {result}");
						break;
					default:
						Software.FileHandler.Log("Client", message);
						break;
				}
			}
		}
		catch(Exception ex)
		{
			Software.FileHandler.Log("Client", $"Read Interrupted with error: {ex.Message}");
			DisconnectSocket();
		}
	}

	private static bool Write(string message)
	{
		try
		{
			_computerClient!.Send(Encoding.UTF8.GetBytes(message));
			return true;
		}
		catch(Exception ex)
		{
			Software.FileHandler.Log("Client", $"Write of message \"{message}\" failed with error: {ex.Message}");
			DisconnectSocket();
			return false;
		}
	}
	
	public static void Boot()
	{
		Helper.RunScript("wake-on-lan", NetworkAdapter.ComputerMac);
	}

	public static bool Shutdown()
	{
		return Write("shutdown");
	}
	
	public static bool Reboot()
	{
		return Write("reboot");
	}
	
	public static bool Notify(string text)
	{
		bool ready = Write("notify");
		return ready && Write(text);
	}
	
	public static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while ((Helper.Ping(NetworkAdapter.ComputerIp) || GetComputerStatus() == EPower.On) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(20000);
		else await Task.Delay(5000);
	}

	private static void CheckConnection(object? sender, EventArgs e) => TestSocket();
	
	private static void DisconnectSocket()
	{
		_computerClient?.Close();
		_computerClient = null;
		UpdateComputerStatus(EPower.Off);
	}
	
	private static bool TestSocket()
	{
		return _computerClient != null && Write("SYN");
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