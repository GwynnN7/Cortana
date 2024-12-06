using Kernel.Hardware.Utility;
using Renci.SshNet;

namespace Kernel.Hardware;

internal static class ComputerService
{
	public static void Boot()
	{
		Helper.RunScript("wake-on-lan", NetworkAdapter.ComputerMac);
		UpdateComputerStatus(EPower.On);
	}

	public static void Shutdown()
	{
		SendCommand("shutdown", true, out _);
		UpdateComputerStatus(EPower.Off);
	}
	
	public static string Reboot()
	{
		SendCommand("reboot", true, out string result);
		return result;
	}
	
	public static string Notify(string text)
	{
		SendCommand($"notify {text}", false, out string result);
		return result;
	}
	
	public static async Task CheckForConnection()
	{
		await Task.Delay(1000);

		DateTime start = DateTime.Now;
		while (Helper.Ping(NetworkAdapter.ComputerIp) && (DateTime.Now - start).Seconds <= 100) await Task.Delay(1500);

		if ((DateTime.Now - start).Seconds < 3) await Task.Delay(20000);
		else await Task.Delay(5000);
	}
	
	private static void SendCommand(string command, bool asRoot, out string result)
	{
		var scriptPath = $"/home/{NetworkAdapter.DesktopUsername}/.config/cortana/cortana-script.sh";

		string usr = asRoot ? NetworkAdapter.DesktopRoot : NetworkAdapter.DesktopUsername;
		string pass = Software.FileHandler.Secrets.DesktopPassword;
		string addr = NetworkAdapter.ComputerIp;

		try
		{
			using var client = new SshClient(addr, usr, pass);
			client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(3);
			client.Connect();

			string cmd = $"{scriptPath} {command}".Trim();
			SshCommand r = client.RunCommand(cmd);

			if (r.ExitStatus == 0) result = r.Result.Trim().Length > 0 && !r.Result.Trim().Equals("0") ? r.Result : "Command executed successfully\n";
			else result = r.Error.Trim().Length > 0 ? r.Error : "There was an error executing the command\n";
			result = result.Trim();

			var log = $"Exit Status: {r.ExitStatus}\nResult: {r.Result}Error: {r.Error}\n----\n";
			Software.FileHandler.Log("SSH", log);

			client.Disconnect();
		}
		catch
		{
			result = "Sorry, I couldn't send the command";
		}
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