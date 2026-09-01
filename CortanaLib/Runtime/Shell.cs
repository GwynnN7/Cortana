using System.Diagnostics;
using System.Text;

namespace CortanaLib.Runtime;

/// Runs shell commands with a timeout and a bounded, merged output. Shared by the Kernel and the desktop agent
public static class Shell
{
	private const int MaxOutput = 3500;

	public static string Path { get; } = Resolve();
	private static string Flag => Path.EndsWith("bash", StringComparison.Ordinal) ? "-lc" : "-c";

	private static string Resolve()
	{
		string? configured = CortanaEnvironment.Read("CORTANA_SHELL");
		if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;

		foreach (string candidate in new[] { "/bin/bash", "/usr/bin/bash", "/bin/sh" })
			if (File.Exists(candidate)) return candidate;

		return "/bin/sh";
	}

	public static ProcessStartInfo StartInfo(string command, bool redirectStdout)
	{
		string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		string localBin = System.IO.Path.Combine(home, ".local", "bin");
		string current = CortanaEnvironment.Read("PATH", "");
		string path = current.Split(':').Contains(localBin) ? current : $"{localBin}:{current}";

		var info = new ProcessStartInfo
		{
			FileName = Path,
			Environment = { ["PATH"] = path },
			RedirectStandardInput = true,
			RedirectStandardOutput = redirectStdout,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		info.ArgumentList.Add(Flag);
		info.ArgumentList.Add(command);
		return info;
	}

	public static Process Start(string command)
	{
		var process = new Process { StartInfo = StartInfo(command, redirectStdout: false) };
		process.Start();
		return process;
	}

	public static void StartDetached(string command, TimeSpan delay)
	{
		_ = Task.Run(async () =>
		{
			await Task.Delay(delay);
			using Process process = Start(command);
			await process.WaitForExitAsync();
		});
	}

	public static async Task<string> Run(string command, TimeSpan timeout)
	{
		using var process = new Process { StartInfo = StartInfo(command, redirectStdout: true) };
		var output = new StringBuilder();

		process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
		process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

		process.Start();
		process.StandardInput.Close();
		process.BeginOutputReadLine();
		process.BeginErrorReadLine();

		using var cts = new CancellationTokenSource(timeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }
			lock (output) output.AppendLine($"[timed out after {timeout.TotalSeconds:0}s]");
		}

		lock (output)
		{
			string text = output.ToString().TrimEnd();
			if (text.Length == 0) return "(no output)";
			return text.Length > MaxOutput ? string.Concat(text.AsSpan(0, MaxOutput), "\n[output truncated]") : text;
		}
	}
}
