using Mono.Unix;
using Processor;

namespace CortanaKernel;

public static class Kernel
{
	private static void Main()
	{
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

		var unixExitSignal = new UnixExitSignal();
		Task.WaitAll(Bootloader.GetSubFunctionsTasks());

		Console.WriteLine("Shutting down Kernel...");
	}
}

public interface IExitSignal
{
	event EventHandler Exit;
}

public class UnixExitSignal : IExitSignal
{
	public event EventHandler? Exit;

	private readonly UnixSignal[] _signals =
	[
		new(Mono.Unix.Native.Signum.SIGTERM), 
		new(Mono.Unix.Native.Signum.SIGINT),
		new(Mono.Unix.Native.Signum.SIGUSR1)
	];

	public UnixExitSignal()
	{
		Task.Factory.StartNew(() =>
		{
			// blocking call to wait for any kill signal
			int index = UnixSignal.WaitAny(_signals, -1);
			Bootloader.StopSubFunctions().Wait();
			Exit?.Invoke(null, EventArgs.Empty);
		});
	}

}