using CortanaAPI;
using Processor;

namespace CortanaKernel;

public class Bootloader
{
	private readonly List<SubFunctionsTasks> _subFunctionTasks = [];

	public int BootSubFunction(ESubFunctions subFunction)
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
		_subFunctionTasks.Add(newTask);
		return subFunctionTask.Id;
	}

	public void StopSubFunctions()
	{
		foreach (SubFunctionsTasks subFunctionTask in _subFunctionTasks) subFunctionTask.CancellationToken.Cancel();
		_subFunctionTasks.Clear();
	}

	public void StopSubFunction(ESubFunctions subFunction)
	{
		foreach (SubFunctionsTasks subFunctionTask in _subFunctionTasks.Where(func => func.SubFunctionType == subFunction))
		{
			subFunctionTask.CancellationToken.Cancel();
			_subFunctionTasks.Remove(subFunctionTask);
			break;
		}
	}

	public void WaitSubFunctions()
	{
		foreach (SubFunctionsTasks subFunctionTask in _subFunctionTasks) subFunctionTask.SubFunctionTask.Wait();
	}

	private readonly record struct SubFunctionsTasks(
		ESubFunctions SubFunctionType,
		Task SubFunctionTask,
		CancellationTokenSource CancellationToken);
}