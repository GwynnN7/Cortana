using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Utility;

public static class Helper
{
	private const int MaxCommandOutput = 3500;

	private static readonly string Shell = ResolveShell();

	private static string ResolveShell()
	{
		string? configured = Environment.GetEnvironmentVariable("CORTANA_SHELL");
		if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

		foreach (string candidate in new[] { "/usr/bin/zsh", "/bin/zsh", "/bin/bash", "/bin/sh" })
			if (File.Exists(candidate)) return candidate;

		return "/bin/sh";
	}

	private static ProcessStartInfo BuildStartInfo(string command, bool redirectStdout)
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string localBin = Path.Combine(home, ".local", "bin");
		string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		string processPath = currentPath.Split(':').Contains(localBin) ? currentPath : $"{localBin}:{currentPath}";

		var info = new ProcessStartInfo
		{
			FileName = Shell,
			Environment = { ["PATH"] = processPath },
			RedirectStandardOutput = redirectStdout,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		info.ArgumentList.Add("-c");
		info.ArgumentList.Add(command);
		return info;
	}

	public static Process RunCommand(string command, bool stdRedirect = false)
	{
		var process = new Process { StartInfo = BuildStartInfo(command, stdRedirect) };
		process.Start();
		return process;
	}

		public static async Task<string> RunCommandWithOutput(string command, TimeSpan timeout)
	{
		using var process = new Process { StartInfo = BuildStartInfo(command, redirectStdout: true) };
		var output = new StringBuilder();

		process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
		process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

		process.Start();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch {  }
			lock (output) output.AppendLine($"[timed out after {timeout.TotalSeconds:0}s]");
		}

		lock (output)
		{
			string text = output.ToString().TrimEnd();
			return text.Length > MaxCommandOutput ? string.Concat(text.AsSpan(0, MaxCommandOutput), "\n[output truncated]") : text;
		}
	}

		public static void DelayCommand(string command)
	{
		Task.Run(async () =>
		{
			await Task.Delay(1000);
			using Process process = RunCommand(command);
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

	public static string FormatTemperature(double temperature, int round = 1) => $"{Math.Round(temperature, round)}°C";

	public static string UnitFor(ESensor sensor) => sensor switch
	{
		ESensor.Temperature => "°C",
		ESensor.Light => " lux",
		ESensor.Humidity => " %",
		ESensor.CO2 => " ppm",
		ESensor.Tvoc => " ppb",
		_ => ""
	};

	public static string UnitFor(ERaspberryInfo info) => info == ERaspberryInfo.Temperature ? "°C" : "";

	public static ESwitchAction ConvertToggle(EDevice device) =>
		HardwareApi.Devices.GetPower(device) == EStatus.On ? ESwitchAction.Off : ESwitchAction.On;
}
