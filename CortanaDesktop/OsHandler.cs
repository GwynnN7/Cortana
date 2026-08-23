using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaDesktop;

internal enum Os
{
	Linux,
	Windows
}

internal static class OsHandler
{
	private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);
	private static readonly Os OperatingSystem;
	private static readonly string ShellPath;
	private static readonly string ShellFlag;

	static OsHandler()
	{
		if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) OperatingSystem = Os.Linux;
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) OperatingSystem = Os.Windows;
		else throw new CortanaException("Unsupported Operating System");

		(ShellPath, ShellFlag) = OperatingSystem == Os.Linux ? (ResolveLinuxShell(), "-c") : ("cmd.exe", "/C");
	}

	private static string ResolveLinuxShell()
	{
		string? configured = Environment.GetEnvironmentVariable("CORTANA_SHELL");
		if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

		foreach (string candidate in new[] { "/usr/bin/fish", "/bin/bash", "/bin/sh" })
			if (File.Exists(candidate)) return candidate;

		return "/bin/sh";
	}

	private static string DecodeCommand(string command, string arg = "")
	{
		bool onLinux = OperatingSystem == Os.Linux;
		return command switch
		{
			"shutdown" => onLinux ? "systemctl poweroff" : "shutdown /s /f /t 0",
			"suspend" => onLinux ? "systemctl suspend -i" : "rundll32.exe powrprof.dll,SetSuspendState 0,1,0",
			"reboot" => onLinux ? "systemctl reboot" : "shutdown /r /f /t 0",
			"system" => onLinux ? "sudo efibootmgr --bootnext 0000 && systemctl reboot" : "shutdown /r /f /t 0",
			"notify" => onLinux ? $"echo '{Escape(arg, '\'')}' | cortana notify" : $"notify-send \"Cortana\" \"{Escape(arg, '"')}\"",
			"cmd" => arg,
			_ => ""
		};
	}

	private static string Escape(string value, char quote) => value.Replace(quote.ToString(), "");

	internal static void ExecuteCommand(string command, string arg = "", bool sendResult = true)
	{
		string commandArg = DecodeCommand(command, arg);
		if (string.IsNullOrEmpty(commandArg)) return;

		_ = Task.Run(async () =>
		{
			try
			{
				string output = await RunAsync(commandArg);
				if (sendResult) CortanaDesktop.Write(string.IsNullOrWhiteSpace(output) ? "Command executed" : output);
			}
			catch (Exception ex)
			{
				DataHandler.Log($"Command '{command}' failed: {ex.Message}");
				if (sendResult) CortanaDesktop.Write($"Command failed: {ex.Message}");
			}
		});
	}

	private static async Task<string> RunAsync(string commandArg)
	{
		var startInfo = new ProcessStartInfo
		{
			FileName = ShellPath,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		startInfo.ArgumentList.Add(ShellFlag);
		startInfo.ArgumentList.Add(commandArg);

		using var process = new Process { StartInfo = startInfo };
		var output = new StringBuilder();

		process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
		process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		using var cts = new CancellationTokenSource(CommandTimeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch {  }
			lock (output) output.AppendLine($"[timed out after {CommandTimeout.TotalSeconds:0}s]");
		}

		lock (output)
		{
			string text = output.ToString().TrimEnd();
			return text.Length > 3500 ? string.Concat(text.AsSpan(0, 3500), "\n[output truncated]") : text;
		}
	}
}
