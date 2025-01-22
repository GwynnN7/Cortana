using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Interfaces;
using Kernel.Software;

namespace Kernel.Hardware.Utility;

internal static class Helper
{
	public static void RunCommand(string command, bool stdRedirect = false)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = "zsh",
			Arguments = $"-c \"{command}\"",
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
	
	public static T? EnumFromString<T>(string value) where T: struct
	{
		value = CapitalizeLetter(value.ToLower());
		bool res = Enum.TryParse(value, out T status);
		return res ? status : null;
	}

	public static string CapitalizeLetter(string word)
	{
		return string.Concat(word[..1].ToUpper(), word.AsSpan(1));
	}
}

