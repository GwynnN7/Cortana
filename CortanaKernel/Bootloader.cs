using CortanaAPI;
using Processor;

namespace CortanaKernel;
public static class Bootloader
{
	private static readonly List<SubFunctionsTasks> SubFunctionTasks = [];

	public static int BootSubFunction(ESubFunctions subFunction)
	{
		Task subFunctionTask = subFunction switch
		{
			ESubFunctions.CortanaApi => Task.Run(CortanaApi.BootCortanaApi),
			ESubFunctions.DiscordBot => Task.Run(DiscordBot.DiscordBot.BootDiscordBot),
			ESubFunctions.TelegramBot => Task.Run(TelegramBot.TelegramBot.BootTelegramBot),
			_ => throw new CortanaException("Unknown SubFunction type, quitting...")
		};
		Func<Task> cancellationFunction = subFunction switch
		{
			ESubFunctions.CortanaApi => CortanaApi.StopCortanaApi,
			ESubFunctions.DiscordBot => DiscordBot.DiscordBot.StopDiscordBot,
			ESubFunctions.TelegramBot => TelegramBot.TelegramBot.StopTelegramBot,
			_ => throw new CortanaException("Unknown SubFunction type, quitting...")
		};
		var newTask = new SubFunctionsTasks(subFunction, subFunctionTask, cancellationFunction);
		SubFunctionTasks.Add(newTask);
		return subFunctionTask.Id;
	}

	public static async Task StopSubFunctions()
	{
		foreach (SubFunctionsTasks subFunctionTask in SubFunctionTasks) await subFunctionTask.CancellationFunction.Invoke();
		SubFunctionTasks.Clear();
	}

	public static async Task StopSubFunction(ESubFunctions subFunction)
	{
		foreach (SubFunctionsTasks subFunctionTask in SubFunctionTasks.Where(func => func.SubFunctionType == subFunction))
		{
			await subFunctionTask.CancellationFunction.Invoke();
			SubFunctionTasks.Remove(subFunctionTask);
			break;
		}
	}

	public static Task[] GetSubFunctionsTasks()
	{
		return SubFunctionTasks.Select(subFunctionTask => subFunctionTask.SubFunctionTask).ToArray();
	}

	private readonly record struct SubFunctionsTasks(
		ESubFunctions SubFunctionType,
		Task SubFunctionTask,
		Func<Task> CancellationFunction);
}