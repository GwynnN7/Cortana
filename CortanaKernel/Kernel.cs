using Processor;

namespace CortanaKernel;

public static class Kernel
{
	private static async Task Main()
	{
		var cts = new CancellationTokenSource();
		Console.CancelKeyPress += (sender, e) =>
		{
			e.Cancel = true;
			cts.Cancel();
		};

		await BootCortana(cts.Token);
	}

	private static Task BootCortana(CancellationToken cancellationToken)
	{
		Console.Clear();
		
		Console.WriteLine($"Compilation completed at {Hardware.GetCpuTemperature()}, loading data for {Hardware.GetLocation()}");
		Console.WriteLine("Initiating Bootloader...");

		int threadId = Bootloader.BootSubFunction(ESubFunctions.CortanaApi);
		Console.WriteLine($"Cortana API ready on Thread {threadId}");

		threadId = Bootloader.BootSubFunction(ESubFunctions.DiscordBot);
		Console.WriteLine($"Discord Bot booting up on Thread {threadId}");

		threadId = Bootloader.BootSubFunction(ESubFunctions.TelegramBot);
		Console.WriteLine($"Telegram Bot booting up on Thread {threadId}");

		Console.WriteLine("Boot Completed, I'm Online!");

		try
		{
			Task.WaitAll(Bootloader.GetSubFunctionsTasks(), cancellationToken);
		}
		catch (OperationCanceledException)
		{
			Console.WriteLine("Signal caught, stopping subfunctions");
			Bootloader.StopSubFunctions();
		}

		Console.WriteLine("Shutting down");
		
		return Task.CompletedTask;
	}
}