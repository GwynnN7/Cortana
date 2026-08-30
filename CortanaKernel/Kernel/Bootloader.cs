using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class Bootloader
{
	private static readonly TimeSpan SystemctlTimeout = TimeSpan.FromSeconds(30);

	private static readonly TimeSpan StatusCacheDuration = TimeSpan.FromSeconds(5);
	private static readonly SemaphoreSlim StatusGate = new(1, 1);
	private static IReadOnlyList<SubfunctionResponse>? _cachedStatuses;
	private static DateTime _cachedStatusesTime = DateTime.MinValue;

	public static async Task<StringResult> Journal(ESubFunctionType type, int lines)
	{
		int wanted = Math.Clamp(lines, 10, 500);

		try
		{
			string output = await Helper.RunCommandWithOutput(
				$"journalctl --user -u {GetServiceName(type)} -n {wanted} --no-pager --reverse --output=short-iso", TimeSpan.FromSeconds(20));

			return StringResult.Success(string.IsNullOrWhiteSpace(output) ? "No journal entries" : output.TrimEnd());
		}
		catch (Exception ex)
		{
			return StringResult.Failure($"Could not read the journal: {ex.Message}");
		}
	}

	private static string GetServiceName(ESubFunctionType type) => type switch
	{
		ESubFunctionType.CortanaKernel => "cortana-kernel",
		ESubFunctionType.CortanaDiscord => "cortana-discord",
		ESubFunctionType.CortanaTelegram => "cortana-telegram",
		ESubFunctionType.CortanaWeb => "cortana-web",
		_ => throw new CortanaException($"Unknown subfunction type: {type}")
	};

	private static async Task<int> RunSystemctl(string action, string serviceName)
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "systemctl",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.StartInfo.ArgumentList.Add("--user");
		process.StartInfo.ArgumentList.Add(action);
		process.StartInfo.ArgumentList.Add(serviceName);

		process.Start();

		using var cts = new CancellationTokenSource(SystemctlTimeout);
		try
		{
			await process.WaitForExitAsync(cts.Token);
			return process.ExitCode;
		}
		catch (OperationCanceledException)
		{
			try { process.Kill(entireProcessTree: true); } catch { }
			DataHandler.Log($"systemctl {action} {serviceName} timed out");
			return -1;
		}
	}

	public static async Task<StringResult> SubfunctionCall(ESubFunctionType type, ESubfunctionAction action)
	{
		string serviceName = GetServiceName(type);
		InvalidateStatusCache();

		switch (action)
		{
			case ESubfunctionAction.Start or ESubfunctionAction.Restart or ESubfunctionAction.Stop when type == ESubFunctionType.CortanaKernel:
				string verb = action.ToString().ToLowerInvariant();
				Helper.DelayCommand($"systemctl --user {verb} {serviceName}");
				return StringResult.Success(DataHandler.Log($"{serviceName} {verb}ing..."));

			case ESubfunctionAction.Start:
			case ESubfunctionAction.Restart:
			case ESubfunctionAction.Stop:
				string command = action.ToString().ToLowerInvariant();
				int exitCode = await RunSystemctl(command, serviceName);
				return exitCode == 0
					? StringResult.Success(DataHandler.Log($"{serviceName} {command} succeeded"))
					: StringResult.Failure(DataHandler.Log($"Failed to {command} {serviceName}"));

			case ESubfunctionAction.Update:
				await Helper.RunCommandWithOutput("cortana git", TimeSpan.FromMinutes(2));
				return await SubfunctionCall(type, ESubfunctionAction.Restart);

			default:
				return StringResult.Failure("Unknown subfunction action");
		}
	}

	public static async Task<StringResult> StopSubfunctions()
	{
		var failed = new List<string>();
		foreach (ESubFunctionType type in Enum.GetValues<ESubFunctionType>())
		{
			if (type == ESubFunctionType.CortanaKernel) continue;
			StringResult stopResult = await SubfunctionCall(type, ESubfunctionAction.Stop);
			if (!stopResult.IsOk) failed.Add(GetServiceName(type));
		}

		return failed.Count == 0
			? StringResult.Success("All subfunctions stopped")
			: StringResult.Failure($"Failed to stop: {string.Join(", ", failed)}");
	}

	public static async Task<bool> IsSubfunctionRunning(ESubFunctionType type) =>
		await RunSystemctl("is-active", GetServiceName(type)) == 0;

	public static async Task<IReadOnlyList<SubfunctionResponse>> GetAllStatuses()
	{
		if (IsStatusCacheValid(out IReadOnlyList<SubfunctionResponse>? cached)) return cached!;

		await StatusGate.WaitAsync();
		try
		{
			if (IsStatusCacheValid(out cached)) return cached!;

			ESubFunctionType[] types = Enum.GetValues<ESubFunctionType>();
			bool[] running = await Task.WhenAll(types.Select(IsSubfunctionRunning));

			_cachedStatuses = types.Select((type, index) => new SubfunctionResponse(type.ToString(), running[index])).ToList();
			_cachedStatusesTime = DateTime.UtcNow;
			return _cachedStatuses;
		}
		finally
		{
			StatusGate.Release();
		}
	}

	private static bool IsStatusCacheValid(out IReadOnlyList<SubfunctionResponse>? cached)
	{
		cached = _cachedStatuses;
		return cached != null && DateTime.UtcNow - _cachedStatusesTime < StatusCacheDuration;
	}

		private static void InvalidateStatusCache()
	{
		_cachedStatusesTime = DateTime.MinValue;
		SystemEvents.Notify();
	}
}
