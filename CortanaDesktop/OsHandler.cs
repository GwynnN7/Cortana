using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CortanaDesktop;

internal enum Os
{
	Linux,
	Windows
}

internal struct OsMacro
{
	private const string BashPath = "/bin/bash";
	private const string CmdPath = "cmd.exe";
	private const string BashArg = "-c";
	private const string CmdArg = "/C";

	public static string GetPath(Os operatingSystem)
	{
		return operatingSystem == Os.Linux ? BashPath : CmdPath;
        
	}
	public static string GetArg(Os operatingSystem)
	{
		return operatingSystem == Os.Linux ? BashArg : CmdArg; 
	}
}

internal static class OsHandler
{
	private static readonly Os OperatingSystem;

	static OsHandler()
	{
		if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) OperatingSystem = Os.Linux;
		else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) OperatingSystem = Os.Windows;
		else throw new Exception("Unsupported Operating System");
	}

	private static string DecodeCommand(string command, string arg = "")
	{
		bool onLinux = OperatingSystem == Os.Linux;
		return command switch
		{
			"shutdown" => onLinux ? "systemctl poweroff" : "shutdown /s /f /t 0",
			"suspend" => onLinux ? "systemctl suspend" : "shutdown /s /f /t 0",
			"reboot" => onLinux ? "systemctl reboot" : "shutdown /r /f /t 0",
			"swap-os" => onLinux ? $"echo {CortanaDesktop.DesktopInfo.DesktopPassword} | sudo -S grub-reboot 1 && reboot" : "shutdown /r /f /t 0",
			"notify" => onLinux ? $"notify-send -u low -a Cortana \'{arg}\'" : $"notify-send \"Cortana\" \"{arg}\"",
			"cmd" => onLinux ? $"echo {CortanaDesktop.DesktopInfo.DesktopPassword} | sudo -S {arg}" : arg,
			_ => ""
		};
	}

	internal static void ExecuteCommand(string command, string arg = "", bool sendResult = true)
	{
		string path = OsMacro.GetPath(OperatingSystem);
		string param = OsMacro.GetArg(OperatingSystem);
		string commandArg = DecodeCommand(command, arg);
		
		var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = path,
				Arguments = $"{param} \"{commandArg}\"",
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			}
		};
		process.Start();
		if(!sendResult) return;
		
		if(process.StandardOutput.EndOfStream && process.StandardError.EndOfStream) CortanaDesktop.Write("Command executed");
		if(!process.StandardOutput.EndOfStream) CortanaDesktop.Write(process.StandardOutput.ReadToEnd());
		if(!process.StandardError.EndOfStream) CortanaDesktop.Write(process.StandardError.ReadToEnd());
	}
}