using System.Diagnostics;
using System.Net.NetworkInformation;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Utility;

public static class Helper
{
	public static Process RunCommand(string command, bool stdRedirect = false)
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string localBin = Path.Combine(home, ".local", "bin");
		string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string processPath = currentPath.Split(':').Contains(localBin) ? currentPath : $"{localBin}:{currentPath}";

		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "/usr/bin/zsh",
				Arguments = $"-c \"{command}\"",
				Environment = { ["PATH"] = processPath },
				RedirectStandardOutput = stdRedirect,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			}
		};
		process.Start();
		return process;
	}

	public static void DelayCommand(string command)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			using var process = RunCommand(command);
			await process.WaitForExitAsync();
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
		return $"{Math.Round(temperature, round)}°C";
	}

	public static ESwitchAction ConvertToggle(EDevice device)
	{
		return HardwareApi.Devices.GetPower(device) == EStatus.On ? ESwitchAction.Off : ESwitchAction.On;
	}
}

