using System.Runtime.InteropServices;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaDesktop;

/// Everything the agent can do to this machine
internal static class DesktopOs
{
	private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(18);
	private static readonly bool OnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	public static async Task<string> Execute(ComputerCommand command, string argument)
	{
		return command switch
		{
			ComputerCommand.Shutdown => await Run(OnWindows ? "shutdown /s /f /t 0" : "systemctl poweroff"),
			ComputerCommand.Reboot => await Run(OnWindows ? "shutdown /r /f /t 0" : "systemctl reboot"),
			ComputerCommand.Suspend => await Run(OnWindows ? "rundll32.exe powrprof.dll,SetSuspendState 0,1,0" : "systemctl suspend -i"),
			ComputerCommand.BootIntoOtherOperatingSystem => await BootIntoOtherOs(),
			ComputerCommand.Notify => await Notify(argument),
			ComputerCommand.RunShellCommand => string.IsNullOrWhiteSpace(argument) ? "No command given" : await Run(argument),
			ComputerCommand.LaunchApplication => await Launch(argument),
			ComputerCommand.CloseApplication => await Close(argument),
			ComputerCommand.SetActivityDetail => Activity.SetDetail(argument),
			_ => $"Unknown command '{command}'"
		};
	}

	private static Task<string> BootIntoOtherOs() =>
		Run(OnWindows ? "shutdown /r /f /t 0" : "sudo efibootmgr --bootnext 0000 && systemctl reboot");

	/// On Linux this goes through the `cortana notify` wrapper
	private static Task<string> Notify(string text)
	{
		string escaped = text.Replace("'", "");

		return Run(OnWindows
			? $"msg * \"{text.Replace("\"", "")}\""
			: $"if command -v cortana >/dev/null 2>&1; then printf '%s' '{escaped}' | cortana notify -t Cortana; " +
			  $"else notify-send -a cortana Cortana '{escaped}'; fi");
	}

	private static async Task<string> Launch(string application)
	{
		if (string.IsNullOrWhiteSpace(application)) return "No application given";

		string requested = application.Trim();
		string binary = requested.Split(' ')[0];

		if (OnWindows) return await Run($"start \"\" {requested}");

		string? resolved = await Resolve(binary);
		if (resolved == null) return $"I could not find anything called '{binary}' on this machine";

		string arguments = requested.Length > binary.Length ? requested[binary.Length..] : "";

		// Detached, with its own session, so it survives the agent restarting
		await Run($"mkdir -p ~/.cache; setsid -f {resolved}{arguments} >>~/.cache/cortana-launch.log 2>&1");
		return $"Launched {resolved}";
	}

	private static async Task<string> Close(string application)
	{
		if (string.IsNullOrWhiteSpace(application)) return "No application given";
		if (OnWindows) return await Run($"taskkill /IM {application.Trim()}.exe /F");

		string running = await Run("ps -eo comm= | sort -u");
		string[] processes = running.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		string? match = BestMatch(application.Trim(), processes);
		if (match == null) return $"Nothing that looks like '{application}' is running";

		// TERM first, to let apps save session
		await Run($"pkill -TERM -x {match}");
		return $"Closed {match}";
	}

	/// Exact name first, then a prefix or contains, then a small edit distance for typos
	private static async Task<string?> Resolve(string binary)
	{
		string check = await Run($"command -v {binary} >/dev/null 2>&1 && echo found || echo missing");
		if (check.Contains("found", StringComparison.Ordinal)) return binary;

		string listing = await Run("compgen -c 2>/dev/null | sort -u");
		string[] candidates = listing.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		return BestMatch(binary, candidates);
	}

	public static string? BestMatch(string wanted, IReadOnlyList<string> candidates)
	{
		if (candidates.Count == 0) return null;

		string needle = wanted.ToLowerInvariant();

		string? exact = candidates.FirstOrDefault(candidate => candidate.Equals(needle, StringComparison.OrdinalIgnoreCase));
		if (exact != null) return exact;

		string? prefix = candidates
			.Where(candidate => candidate.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
			.MinBy(candidate => candidate.Length);
		if (prefix != null) return prefix;

		string? contains = candidates
			.Where(candidate => candidate.Contains(needle, StringComparison.OrdinalIgnoreCase))
			.MinBy(candidate => candidate.Length);
		if (contains != null) return contains;

		// One edit per four characters for typos
		int tolerance = Math.Max(1, needle.Length / 4);
		return candidates
			.Select(candidate => (Candidate: candidate, Distance: Distance(needle, candidate.ToLowerInvariant())))
			.Where(entry => entry.Distance <= tolerance)
			.OrderBy(entry => entry.Distance)
			.ThenBy(entry => entry.Candidate.Length)
			.Select(entry => entry.Candidate)
			.FirstOrDefault();
	}

	private static int Distance(string left, string right)
	{
		int[] previous = [.. Enumerable.Range(0, right.Length + 1)];
		int[] current = new int[right.Length + 1];

		for (var i = 1; i <= left.Length; i++)
		{
			current[0] = i;

			for (var j = 1; j <= right.Length; j++)
				current[j] = Math.Min(
					Math.Min(previous[j] + 1, current[j - 1] + 1),
					previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));

			(previous, current) = (current, previous);
		}

		return previous[right.Length];
	}

	private static Task<string> Run(string command) =>
		OnWindows ? RunWindows(command) : Shell.Run(command, CommandTimeout);

	private static async Task<string> RunWindows(string command)
	{
		var info = new System.Diagnostics.ProcessStartInfo
		{
			FileName = "cmd.exe",
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			UseShellExecute = false,
			CreateNoWindow = true
		};
		info.ArgumentList.Add("/C");
		info.ArgumentList.Add(command);

		using var process = new System.Diagnostics.Process { StartInfo = info };
		process.Start();

		string output = await process.StandardOutput.ReadToEndAsync();
		string errors = await process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

		string text = (output + errors).Trim();
		return text.Length == 0 ? "(no output)" : text;
	}
}
