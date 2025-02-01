using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Subfunctions;

public static class Bootloader
{
	private static readonly List<SubFunction> RunningSubFunctions = [];

	public static async Task<StringResult> HandleSubFunction(ESubFunctionType type, ESubfunctionAction action)
	{
		switch (action)
		{
			case ESubfunctionAction.Stop:
				return await StopSubFunction(type);
			case ESubfunctionAction.Restart:
				return await RebootSubFunction(type);
			case ESubfunctionAction.Update:
				await Helper.AwaitCommand("cortana --update");
				return await RebootSubFunction(type);
			case ESubfunctionAction.Start:
				return await BootSubFunction(type);
			default:
				return StringResult.Failure("Subfunction not found");
		}
	}
	
	private static async Task<StringResult> BootSubFunction(ESubFunctionType subFunctionType)
	{
		string projectName = subFunctionType.ToString();

		var process = new SubFunction();
		process.Type = subFunctionType;
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"run --project {FileHandler.GetPath(EDirType.Projects)}/{projectName}/{projectName}.csproj",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.EnableRaisingEvents = true;
		
		process.Exited += async (_, _) => {
			if(process.ShuttingDown) return;
			await Task.Delay(1000);
			Console.WriteLine($"{projectName} exited. Restarting...");
			RunningSubFunctions.Remove(process);
			await BootSubFunction(process.Type);
		};

		try
		{
			process.Start();
			RunningSubFunctions.Add(process);
			await Task.Delay(500);
			return StringResult.Success($"{projectName} started with pid {process.Id}.");
		}
		catch
		{
			return StringResult.Failure($"Failed to start {projectName}");
		}
	}

	private static async Task<StringResult> RebootSubFunction(ESubFunctionType subFuncType)
	{
		await StopSubFunction(subFuncType);
		await Task.Delay(500);
		return await BootSubFunction(subFuncType);
	}

	private static async Task TryKillProcess(Process process)
	{
		if(process.HasExited) return;
		process.Kill(true);
		try
		{
			await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
		}
		catch (TimeoutException)
		{
			Process.Start("kill", "-9 " + process.Id);
		}
	}
	
	private static async Task<StringResult> StopSubFunction(SubFunction subFunction)
	{
		if(subFunction.HasExited) return StringResult.Failure($"Failed to stop subfunction {subFunction.Type}");
		subFunction.ShuttingDown = true;
		
		foreach (Process process in Process.GetProcessesByName(subFunction.Type.ToString()))
		{
			await TryKillProcess(process);
		}
		await TryKillProcess(subFunction);
		
		RunningSubFunctions.Remove(subFunction);
		return StringResult.Success($"{subFunction.Type} stopped");
	}
	
	private static async Task<StringResult> StopSubFunction(ESubFunctionType subFuncType)
	{
		foreach (SubFunction subFunction in RunningSubFunctions.Where(func => func.Type == subFuncType))
		{
			return await StopSubFunction(subFunction);
		}
		return StringResult.Failure($"Failed to stop subfunction {subFuncType}");
	}

	public static async Task StopSubFunctions()
	{
		List<Task<StringResult>> subfunctionsToStop = RunningSubFunctions.Select(StopSubFunction).ToList();
		await Task.WhenAll(subfunctionsToStop);
		RunningSubFunctions.Clear();
	}

	public static bool IsSubfunctionActive(ESubFunctionType subFuncType)
	{
		return RunningSubFunctions.Any(func => func.Type == subFuncType && !func.HasExited);
	}
}