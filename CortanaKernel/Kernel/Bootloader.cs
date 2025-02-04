using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class Bootloader
{
	private static readonly Dictionary<ESubFunctionType, Subfunction> RunningSubFunctions = new();

	public static async Task<StringResult> SubfunctionCall(ESubFunctionType type, ESubfunctionAction action)
	{
		switch (action)
		{
			case ESubfunctionAction.Build:
			{
				StringResult stopResult = await StopSubfunction(type);
				if (!stopResult.IsOk) return stopResult;
				StringResult buildResult = await BuildSubfunction(type);
				return !buildResult.IsOk ? buildResult : BootSubFunction(type);
			}
			case ESubfunctionAction.Boot:
			{
				StringResult stopResult = await StopSubfunction(type);
				return !stopResult.IsOk ? stopResult : BootSubFunction(type);
			}
			case ESubfunctionAction.Reboot:
			{
				await Helper.RunCommand("cortana update").WaitForExitAsync();
				return await SubfunctionCall(type, ESubfunctionAction.Build);
			}
			case ESubfunctionAction.Stop:
				return await StopSubfunction(type);
			default:
				return StringResult.Failure("Unknown subfunction type");
		}
	}

	private static async Task<StringResult> BuildSubfunction(ESubFunctionType type)
	{
		string projectName = type.ToString();
		
		var process = new Process();
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"build {projectName} -o {projectName}/out --artifacts-path {projectName}/out/lib",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.EnableRaisingEvents = true;
		
		DataHandler.Log(nameof(CortanaKernel), $"Building subfunction {type}");
		try
		{
			process.Start();
			await process.WaitForExitAsync();
			if (process.ExitCode != 0) throw new CortanaException();
			return StringResult.Success(
				DataHandler.Log(nameof(CortanaKernel), $"Subfunction {type} built!"));
		}
		catch
		{
			return StringResult.Failure(
				DataHandler.Log(nameof(CortanaKernel),$"Failed to build subfunction {type}"));
		}
	}
	
	private static StringResult BootSubFunction(ESubFunctionType type)
	{
		string projectName = type.ToString();

		var process = new Subfunction();
		process.Type = type;
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "zsh",
			Arguments = $"-c \"{projectName}/out/{projectName}\"",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.EnableRaisingEvents = true;
		switch (type)
		{
			case ESubFunctionType.CortanaDiscord:
				process.StartInfo.Environment.Add("CORTANA_DISCORD_TOKEN", DataHandler.Env("CORTANA_DISCORD_TOKEN"));
				process.StartInfo.Environment.Add("CORTANA_IGDB_CLIENT", DataHandler.Env("CORTANA_IGDB_CLIENT"));
				process.StartInfo.Environment.Add("CORTANA_IGDB_SECRET", DataHandler.Env("CORTANA_IGDB_SECRET"));
				break;
			case ESubFunctionType.CortanaTelegram:
				process.StartInfo.Environment.Add("CORTANA_TELEGRAM_TOKEN", DataHandler.Env("CORTANA_TELEGRAM_TOKEN"));
				break;
			case ESubFunctionType.CortanaWeb:
				process.StartInfo.Environment.Add("ASPNETCORE_ENVIRONMENT", DataHandler.Env("ASPNETCORE_ENVIRONMENT"));
				process.StartInfo.Environment.Add("ASPNETCORE_URLS", DataHandler.Env("ASPNETCORE_URLS"));
				break;
		}
		process.StartInfo.Environment.Add("CORTANA_API", DataHandler.Env("CORTANA_API"));
		process.StartInfo.Environment.Add("CORTANA_PATH", DataHandler.Env("CORTANA_PATH"));
		
		process.Exited += async (_, _) => {
			if(process.ShuttingDown) return;
			await Task.Delay(1000);
			DataHandler.Log(nameof(CortanaKernel), $"{projectName} exited. Restarting...");
			RunningSubFunctions.Remove(process.Type);
			BootSubFunction(process.Type);
		};

		try
		{
			DataHandler.Log(nameof(CortanaKernel), $"Starting subfunction {type}");
			process.Start();
			RunningSubFunctions.Add(process.Type, process);
			return StringResult.Success(
				DataHandler.Log(nameof(CortanaKernel),$"{projectName} started with pid {process.Id}."));
		}
		catch
		{
			return StringResult.Failure(
				DataHandler.Log(nameof(CortanaKernel),$"Failed to start {projectName}"));
		}
	}
	
	private static async Task<StringResult> KillSubfunction(Subfunction subfunction)
	{
		if(subfunction.HasExited) return StringResult.Success($"{subfunction.Type} subfunction already exited");
		subfunction.ShuttingDown = true;
		
		Task stoppingTask = subfunction.WaitForExitAsync();
		string[] signals = ["SIGUSR1", "SIGINT", "SIGKILL"];
		foreach (var signal in signals)
		{
			if(subfunction.HasExited) break;
			Process.Start("pkill", $"-{signal} -i {subfunction.Type}");
			Task winner = await Task.WhenAny(stoppingTask, Task.Delay(TimeSpan.FromSeconds(2)));
			if (winner != stoppingTask && !subfunction.HasExited) continue;
			
			RunningSubFunctions.Remove(subfunction.Type);
			return StringResult.Success(
				DataHandler.Log(nameof(CortanaKernel),$"{subfunction.Type} stopped with signal {signal}"));
		}
		return StringResult.Failure(
			DataHandler.Log(nameof(CortanaKernel),$"Failed to stop {subfunction.Type}")); 
	}
	
	private static async Task<StringResult> StopSubfunction(ESubFunctionType subFuncType)
	{
		if (!RunningSubFunctions.TryGetValue(subFuncType, out Subfunction? subFunction))
		{
			return StringResult.Success($"{subFuncType} subfunction not running");
		}
		return await KillSubfunction(subFunction);
	}

	public static async Task<StringResult> StopSubfunctions()
	{
		bool result = true;
		foreach (ESubFunctionType type in Enum.GetValues<ESubFunctionType>())
		{
			StringResult stopResult = await StopSubfunction(type);
			result &= stopResult.IsOk;
		}
		return result ? StringResult.Success("All subfunction stopped") : StringResult.Failure("Failed to stop one or more subfunctions");
	}

	public static bool IsSubfunctionActive(ESubFunctionType subFuncType)
	{
		if (RunningSubFunctions.TryGetValue(subFuncType, out Subfunction? subFunction))
		{
			return !subFunction.HasExited;
		}
		return false;
	}
}