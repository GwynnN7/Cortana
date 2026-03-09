using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class Bootloader
{
	private static string GetServiceName(ESubFunctionType type)
	{
		return type switch
		{
			ESubFunctionType.CortanaKernel => "cortana-kernel",
			ESubFunctionType.CortanaDiscord => "cortana-discord",
			ESubFunctionType.CortanaTelegram => "cortana-telegram",
			ESubFunctionType.CortanaWeb => "cortana-web",
			_ => throw new CortanaException($"Unknown subfunction type: {type}")
		};
	}

	private static async Task<int> RunSystemctl(string action, string serviceName)
	{
		using var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "systemctl",
			Arguments = $"--user {action} {serviceName}",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.Start();
		await process.WaitForExitAsync();
		return process.ExitCode;
	}

	public static async Task<StringResult> SubfunctionCall(ESubFunctionType type, ESubfunctionAction action)
	{
		string serviceName = GetServiceName(type);

		switch (action)
		{
			case ESubfunctionAction.Reboot when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand($"systemctl --user restart {serviceName}");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel), "Kernel rebooting..."));
			case ESubfunctionAction.Reboot:
				{
					int exitCode = await RunSystemctl("restart", serviceName);
					return exitCode == 0
						? StringResult.Success(DataHandler.Log(nameof(CortanaKernel), $"{type} rebooted"))
						: StringResult.Failure(DataHandler.Log(nameof(CortanaKernel), $"Failed to reboot {type}"));
				}
			case ESubfunctionAction.Restart when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand($"systemctl --user restart {serviceName}");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel), "Kernel restarting..."));
			case ESubfunctionAction.Restart:
				{
					int exitCode = await RunSystemctl("restart", serviceName);
					return exitCode == 0
						? StringResult.Success(DataHandler.Log(nameof(CortanaKernel), $"{type} restarted"))
						: StringResult.Failure(DataHandler.Log(nameof(CortanaKernel), $"Failed to restart {type}"));
				}
			case ESubfunctionAction.Update when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand("cortana update");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel), "Kernel updating..."));
			case ESubfunctionAction.Update:
				{
					await Helper.RunCommand("cortana git").WaitForExitAsync();
					return await SubfunctionCall(type, ESubfunctionAction.Reboot);
				}
			case ESubfunctionAction.Stop:
				return await StopSubfunction(type);
			default:
				return StringResult.Failure("Unknown subfunction action");
		}
	}

	private static async Task<StringResult> StopSubfunction(ESubFunctionType type)
	{
		if (type == ESubFunctionType.CortanaKernel)
		{
			Helper.DelayCommand($"systemctl --user stop {GetServiceName(type)}");
			return StringResult.Success(DataHandler.Log(nameof(CortanaKernel), "Kernel stopping..."));
		}

		string serviceName = GetServiceName(type);
		int exitCode = await RunSystemctl("stop", serviceName);
		return exitCode == 0
			? StringResult.Success(DataHandler.Log(nameof(CortanaKernel), $"{type} stopped"))
			: StringResult.Failure(DataHandler.Log(nameof(CortanaKernel), $"Failed to stop {type}"));
	}

	public static async Task<StringResult> StopSubfunctions()
	{
		bool result = true;
		foreach (ESubFunctionType type in Enum.GetValues<ESubFunctionType>())
		{
			if (type == ESubFunctionType.CortanaKernel) continue;
			StringResult stopResult = await StopSubfunction(type);
			result &= stopResult.IsOk;
		}
		return result ? StringResult.Success("All subfunctions stopped") : StringResult.Failure("Failed to stop one or more subfunctions");
	}

	public static async Task<bool> IsSubfunctionRunning(ESubFunctionType type)
	{
		string serviceName = GetServiceName(type);
		int exitCode = await RunSystemctl("is-active", serviceName);
		return exitCode == 0;
	}
}