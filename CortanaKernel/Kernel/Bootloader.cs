using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class Bootloader
{
	private static readonly Dictionary<ESubFunctionType, Process> RunningSubFunctions = new();

	public static void LoadKernel()
	{
		Process process = Process.GetProcessesByName("CortanaKernel").FirstOrDefault() ?? throw new CortanaException("Cannot find CortanaKernel process");
		RunningSubFunctions.Add(ESubFunctionType.CortanaKernel, process);
		EnvService.SetEnv(process);
	}
	
	public static async Task<StringResult> SubfunctionCall(ESubFunctionType type, ESubfunctionAction action)
	{
		switch (action)
		{
			case ESubfunctionAction.Reboot when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand("cortana reboot");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel),"Kernel rebooting, shutting down..."));
			case ESubfunctionAction.Reboot:
			{
				StringResult stopResult = await StopSubfunction(type);
				if (!stopResult.IsOk) return stopResult;
				StringResult buildResult = await BuildSubfunction(type);
				return !buildResult.IsOk ? buildResult : BootSubFunction(type);
			}
			case ESubfunctionAction.Restart when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand("cortana restart");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel),"Kernel restarting, shutting down..."));
			case ESubfunctionAction.Restart:
			{
				StringResult stopResult = await StopSubfunction(type);
				return !stopResult.IsOk ? stopResult : BootSubFunction(type);
			}
			case ESubfunctionAction.Update when type == ESubFunctionType.CortanaKernel:
				Helper.DelayCommand("cortana update");
				return StringResult.Success(
					DataHandler.Log(nameof(CortanaKernel),"Kernel updating, shutting down..."));
			case ESubfunctionAction.Update:
			{
				await Helper.RunCommand("cortana git").WaitForExitAsync();
				return await SubfunctionCall(type, ESubfunctionAction.Reboot);
			}
			case ESubfunctionAction.Stop:
				return await StopSubfunction(type);
			default:
				return StringResult.Failure("Unknown subfunction type");
		}
	}

	private static async Task<StringResult> BuildSubfunction(ESubFunctionType type)
	{
		if(type == ESubFunctionType.CortanaKernel) return StringResult.Failure("Cannot build Kernel from here");
		
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
			if (process.ExitCode != 0) throw new CortanaException($"Failed to build subfunction {type}");
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
		if(type == ESubFunctionType.CortanaKernel) return StringResult.Failure("Cannot boot Kernel from here");
		
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
		
		EnvService.SetEnv(process);
		
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
	
	private static async Task<StringResult> KillSubfunction(Process subfunction, ESubFunctionType type)
	{
		if(subfunction.HasExited) return StringResult.Success($"{type} subfunction already exited");
		
		if (subfunction is Subfunction subfunctionProcess)
		{
			subfunctionProcess.ShuttingDown = true;
		}
		
		Task stoppingTask = subfunction.WaitForExitAsync();
		string[] signals = ["SIGUSR1", "SIGINT", "SIGKILL"];
		foreach (var signal in signals)
		{
			if(subfunction.HasExited) break;
			Process.Start("pkill", $"-{signal} -i {type}");
			Task winner = await Task.WhenAny(stoppingTask, Task.Delay(TimeSpan.FromSeconds(2)));
			if (winner != stoppingTask && !subfunction.HasExited) continue;
			
			RunningSubFunctions.Remove(type);
			return StringResult.Success(
				DataHandler.Log(nameof(CortanaKernel),$"{type} stopped with signal {signal}"));
		}
		return StringResult.Failure(
			DataHandler.Log(nameof(CortanaKernel),$"Failed to stop {type}")); 
	}
	
	private static async Task<StringResult> StopSubfunction(ESubFunctionType subFuncType)
	{
		if (!RunningSubFunctions.TryGetValue(subFuncType, out Process? subFunction))
		{
			return StringResult.Success($"{subFuncType} subfunction not running");
		}
		return await KillSubfunction(subFunction, subFuncType);
	}

	public static async Task<StringResult> StopSubfunctions()
	{
		bool result = true;
		foreach (ESubFunctionType type in Enum.GetValues<ESubFunctionType>())
		{
			if(type == ESubFunctionType.CortanaKernel) continue;
			StringResult stopResult = await StopSubfunction(type);
			result &= stopResult.IsOk;
		}
		return result ? StringResult.Success("All subfunction stopped") : StringResult.Failure("Failed to stop one or more subfunctions");
	}

	public static bool IsSubfunctionRunning(ESubFunctionType subFuncType)
	{
		if (RunningSubFunctions.TryGetValue(subFuncType, out Process? subFunction))
		{
			return !subFunction.HasExited;
		}
		return false;
	}
}