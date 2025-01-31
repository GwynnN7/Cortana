using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using CortanaKernel.Hardware.Structures;
using Utility;
using Utility.Structures;

namespace CortanaKernel.Hardware.Utility;

public static class Helper
{
	public static readonly string StoragePath =  Path.Combine(FileHandler.ProjectStoragePath, "Config/Hardware/");
	public static Process RunCommand(string command, bool stdRedirect = false)
	{
		var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "zsh",
			Arguments = $"-c \"{command}\"",
			RedirectStandardOutput = stdRedirect,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		process.Start();
		return process;
	}
	
	public static async Task AwaitCommand(string command)
	{
		Process process = RunCommand(command);
		await process.WaitForExitAsync();
	}
	
	public static bool Ping(string ip)
	{
		using var pingSender = new Ping();
		try
		{
			PingReply reply = pingSender.Send(ip, 2000);
			return reply.Status == IPStatus.Success;
		}
		catch
		{
			return false;
		}
	}
	
	public static string FormatTemperature(double temperature, int round = 1)
	{
		var tempFormat = $"{Math.Round(temperature, round).ToString(CultureInfo.InvariantCulture)}°C";
		return tempFormat;
	}

	public static EPowerAction ConvertToggle(EDevice device)
	{
		return HardwareApi.Devices.GetPower(device) == EPowerStatus.On ? EPowerAction.Off : EPowerAction.On;
	}
}

