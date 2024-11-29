using Processor;

namespace CortanaKernel;

public static class Kernel
{
	private static void Main()
	{
		Console.CancelKeyPress += (sender, e) =>
		{
			Bootloader.StopSubFunctions().Wait();
		};

		BootCortana();
	}

	private static void BootCortana()
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

		Task.WaitAll(Bootloader.GetSubFunctionsTasks());

		Console.WriteLine("Shutting down Kernel...");
	}
}