using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using Kernel.Hardware.Interfaces;
using Kernel.Software;

namespace Kernel.Hardware.Utility;

internal static class Helper
{
	public static void RunScript(string fileName, string args = "", bool stdRedirect = false)
	{
		string filePath = Path.Combine(FileHandler.ProjectStoragePath, $"Scripts/{fileName}.sh");
		
		Process.Start(new ProcessStartInfo
		{
			FileName = "zsh",
			Arguments = $"-c \"{filePath} {args}\"",
			RedirectStandardOutput = stdRedirect,
			UseShellExecute = false,
			CreateNoWindow = true
		});
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
		return HardwareAdapter.GetDevicePower(device) == EPower.On ? EPowerAction.Off : EPowerAction.On;
	}
	
	public static EDevice? HardwareDeviceFromString(string device)
	{
		device = device.ToLower();
		device = string.Concat(device[0].ToString().ToUpper(), device.AsSpan(1));
		bool res = Enum.TryParse(device, out EDevice status);
		return res ? status : null;
	}
	public static EPowerAction? PowerActionFromString(string action)
	{
		action = action.ToLower();
		action = string.Concat(action[0].ToString().ToUpper(), action.AsSpan(1));
		bool res = Enum.TryParse(action, out EPowerAction status);
		return res ? status : null;
	}
}

