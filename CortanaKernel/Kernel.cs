using Processor;

namespace CortanaKernel;

public static class Kernel
{
	private static void Main()
	{
		BootCortana().GetAwaiter().GetResult();
	}

	private static async Task<Task> BootCortana()
	{
		Console.Clear();

		Console.WriteLine("Booting up...");

		Console.WriteLine($"Compilation completed at {Hardware.GetCpuTemperature()}, loading data for {Hardware.GetLocation()}");
		await Task.Delay(500);
		
		Console.WriteLine("Initiating Bootloader...");

		int threadId = Bootloader.BootSubFunction(ESubFunctions.CortanaApi);
		Console.WriteLine($"Cortana API ready on Thread {threadId}");
		await Task.Delay(500);

		threadId = Bootloader.BootSubFunction(ESubFunctions.DiscordBot);
		Console.WriteLine($"Discord Bot booting up on Thread {threadId}");
		await Task.Delay(500);

		threadId = Bootloader.BootSubFunction(ESubFunctions.TelegramBot);
		Console.WriteLine($"Telegram Bot booting up on Thread {threadId}");
		await Task.Delay(500);

		Console.WriteLine("Boot Completed, I'm Online!");
		Console.CancelKeyPress += (_, __) =>
		{
			Console.WriteLine("Signal catched, stopping subfunctions");
			Bootloader.StopSubFunctions();
		};
		Bootloader.WaitSubFunctions();

		Console.WriteLine("Shutting down");
		Bootloader.StopSubFunctions();

		return Task.CompletedTask;
	}
}