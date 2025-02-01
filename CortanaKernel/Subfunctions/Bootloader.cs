using System.Diagnostics;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Subfunctions;

public static class Bootloader
{
	private static readonly Dictionary<ESubFunctionType, SubFunction> RunningSubFunctions = new();

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
		if (RunningSubFunctions.ContainsKey(subFunctionType))
		{
			return StringResult.Failure("Subfunction already running".Log());
		}
		
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
			$"{projectName} exited. Restarting...".Log();
			RunningSubFunctions.Remove(process.Type);
			await BootSubFunction(process.Type);
		};

		try
		{
			process.Start();
			RunningSubFunctions.Add(process.Type, process);
			await Task.Delay(500);
			return StringResult.Success($"{projectName} started with pid {process.Id}.".Log());
		}
		catch
		{
			return StringResult.Failure($"Failed to start {projectName}".Log());
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

		Task stoppingTask = process.WaitForExitAsync();
		Task winner = await Task.WhenAny(stoppingTask, Task.Delay(TimeSpan.FromMilliseconds(1500)));
		if (winner != stoppingTask)
		{
			Process.Start("kill", "-9 " + process.Id);
		}
	}
	
	private static async Task<StringResult> StopSubFunction(SubFunction subFunction)
	{
		if(subFunction.HasExited) return StringResult.Failure($"Failed to stop subfunction {subFunction.Type}".Log());
		subFunction.ShuttingDown = true;

		foreach (Process process in Process.GetProcessesByName(subFunction.Type.ToString()))
		{
			await TryKillProcess(process);
		}
		await TryKillProcess(subFunction);
		
		RunningSubFunctions.Remove(subFunction.Type);
		return StringResult.Success($"{subFunction.Type} stopped".Log());
	}
	
	private static async Task<StringResult> StopSubFunction(ESubFunctionType subFuncType)
	{
		if (!RunningSubFunctions.TryGetValue(subFuncType, out SubFunction? subFunction))
		{
			return StringResult.Failure($"Failed to stop {subFuncType}, subfunction not running".Log());
		}
		return await StopSubFunction(subFunction);
	}

	public static async Task StopSubFunctions()
	{
		foreach (ESubFunctionType type in Enum.GetValues<ESubFunctionType>())
		{
			await StopSubFunction(type);
		}
	}

	public static bool IsSubfunctionActive(ESubFunctionType subFuncType)
	{
		if (RunningSubFunctions.TryGetValue(subFuncType, out SubFunction? subFunction))
		{
			return !subFunction.HasExited;
		}

		return false;
	}
}