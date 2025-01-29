using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Mono.Unix;
using Mono.Unix.Native;

namespace Bootloader;

public static class Cortana
{
	private static readonly UnixSignal[] Signals =
	[
		new(Signum.SIGTERM), 
		new(Signum.SIGINT),
		new(Signum.SIGUSR1)
	];

	private static void Main()
	{
		Console.Clear();
		
		Console.WriteLine($"Compilation completed at {HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Temperature)}, loading data for {HardwareApi.Raspberry.GetHardwareInfo(EHardwareInfo.Location)}");
		Console.WriteLine("Initiating Bootloader...");

		int threadId = Bootloader.BootSubFunction(ESubFunctions.CortanaApi);
		Console.WriteLine($"Cortana API ready on Thread {threadId}");

		threadId = Bootloader.BootSubFunction(ESubFunctions.DiscordBot);
		Console.WriteLine($"Discord Bot booting up on Thread {threadId}");

		threadId = Bootloader.BootSubFunction(ESubFunctions.TelegramBot);
		Console.WriteLine($"Telegram Bot booting up on Thread {threadId}");

		Console.WriteLine("Boot Completed, I'm Online!");
		
		Task.Run(async () =>
		{
			UnixSignal.WaitAny(Signals, Timeout.Infinite);
			await Bootloader.StopSubFunctions();
			HardwareApi.ShutdownServices();
		});
		
		Task.WaitAll(Bootloader.GetSubFunctionsTasks());

		Console.WriteLine("Shutting down...");
	}
}