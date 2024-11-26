using CortanaAPI;
using Processor;

namespace CortanaKernel;

public static class Bootloader
{
	private static readonly List<SubFunctionsTasks> SubFunctionTasks = [];

	public static int BootSubFunction(ESubFunctions subFunction)
	{
		var token = new CancellationTokenSource();
		Task subFunctionTask = subFunction switch
		{
			ESubFunctions.CortanaApi => Task.Run(CortanaApi.BootCortanaApi, token.Token),
			ESubFunctions.DiscordBot => Task.Run(DiscordBot.DiscordBot.BootDiscordBot, token.Token),
			ESubFunctions.TelegramBot => Task.Run(TelegramBot.TelegramBot.BootTelegramBot, token.Token),
			_ => throw new CortanaException("Unknown SubFunction type, quitting...")
		};
		subFunctionTask.ContinueWith(_ =>
		{
			token.Dispose();
			subFunctionTask.Dispose();
		});
		var newTask = new SubFunctionsTasks(subFunction, subFunctionTask, token);
		SubFunctionTasks.Add(newTask);
		return subFunctionTask.Id;
	}

	public static void StopSubFunctions()
	{
		foreach (SubFunctionsTasks subFunctionTask in SubFunctionTasks) subFunctionTask.CancellationToken.Cancel();
		SubFunctionTasks.Clear();
	}

	public static void StopSubFunction(ESubFunctions subFunction)
	{
		foreach (SubFunctionsTasks subFunctionTask in SubFunctionTasks.Where(func => func.SubFunctionType == subFunction))
		{
			subFunctionTask.CancellationToken.Cancel();
			SubFunctionTasks.Remove(subFunctionTask);
			break;
		}
	}

	public static void WaitSubFunctions()
	{
		foreach (SubFunctionsTasks subFunctionTask in SubFunctionTasks) subFunctionTask.SubFunctionTask.Wait();
	}

	private readonly record struct SubFunctionsTasks(
		ESubFunctions SubFunctionType,
		Task SubFunctionTask,
		CancellationTokenSource CancellationToken);
}