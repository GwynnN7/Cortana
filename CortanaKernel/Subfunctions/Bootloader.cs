using System.Diagnostics;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.Subfunctions;

public static class Bootloader
{
	private static readonly List<SubFunction> RunningSubFunctions = [];

	public static void BootSubFunction(ESubFunctionType subFunctionType)
	{
		string projectName = subFunctionType.ToString();
		
		var process = new SubFunction();
		process.Type = subFunctionType;
		process.StartInfo = new ProcessStartInfo
		{
			FileName = "dotnet",
			Arguments = $"run --project {projectName}/{projectName}.csproj",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true
		};
		process.EnableRaisingEvents = true;
		process.Exited += async (_, _) => {
			if(process.ShuttingDown) return;
			await Task.Delay(2000);
			Console.WriteLine($"{projectName} exited. Restarting...");
			RunningSubFunctions.Remove(process);
			BootSubFunction(process.Type);
		};

		try
		{
			process.Start();
			RunningSubFunctions.Add(process);
			Console.WriteLine($"{projectName} started successfully with pid {process.Id}.");
		}
		catch
		{
			Console.WriteLine($"Failed to start {projectName}");
		}
	}

	public static async Task RebootSubFunction(ESubFunctionType subFuncType)
	{
		await Helper.AwaitCommand("cortana --update");
		await StopSubFunction(subFuncType);
		BootSubFunction(subFuncType);
	}

	private static async Task StopSubFunction(SubFunction subFunction)
	{
		subFunction.ShuttingDown = true;
		subFunction.Kill();
		await subFunction.WaitForExitAsync();
		Console.WriteLine($"{subFunction.Type} stopped");
		RunningSubFunctions.Remove(subFunction);
	}
	
	public static async Task StopSubFunction(ESubFunctionType subFuncType)
	{
		foreach (SubFunction subFunction in RunningSubFunctions.Where(func => func.Type == subFuncType && !func.HasExited))
		{
			await StopSubFunction(subFunction);
			break;
		}
	}

	public static async Task StopSubFunctions()
	{
		foreach (SubFunction subFunction in RunningSubFunctions.Where(func => !func.HasExited))
		{
			await StopSubFunction(subFunction);
		}
		RunningSubFunctions.Clear();
	}
}