using CortanaKernel.API;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Subfunctions;
using CortanaLib.Structures;
using Mono.Unix;
using Mono.Unix.Native;

namespace CortanaKernel;

public static class Cortana
{
    private static readonly UnixSignal[] Signals =
    [
        new(Signum.SIGTERM), 
        new(Signum.SIGINT),
        new(Signum.SIGUSR1)
    ];

    private static async Task Main()
    {
        Console.Clear();
		
        Console.WriteLine("Compilation completed");
        
        Console.WriteLine("Initiating Hardware...");
        StringResult temp = HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.Temperature);
        StringResult location = HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.Location);

        if (!temp.IsOk || !location.IsOk)
        {
            Console.WriteLine("Failed to initialize hardware, quitting...");
            return;
        }
        Console.WriteLine($"CPU Temperature: {temp.Value}, loaded data for {location.Value}");
        
        Console.WriteLine("Initializing API...");
        
        
        Console.WriteLine("Initiating Bootloader...");

        await Bootloader.HandleSubFunction(ESubFunctionType.CortanaWeb, ESubfunctionAction.Start);
        await Bootloader.HandleSubFunction(ESubFunctionType.CortanaTelegram, ESubfunctionAction.Start);
        await Bootloader.HandleSubFunction(ESubFunctionType.CortanaDiscord, ESubfunctionAction.Start);
        Task apiTask = Task.Run(async () => await CortanaApi.RunAsync());
        Task shutdownTask = Task.Run(async () => await WaitForSignal());
        Console.WriteLine("Boot Completed, I'm Online!");

        await Task.WhenAll(apiTask, shutdownTask);
        Console.WriteLine("Shutting Down...");
    }

    private static async Task WaitForSignal()
    {
        UnixSignal.WaitAny(Signals, Timeout.Infinite);
        Console.WriteLine("Initiating Termination Sequence...");
        Task stopHardware = Task.Run(async () =>
        {
            await Task.Delay(500);
            NotificationHandler.Stop();
            HardwareApi.ShutdownService();
        });
        await Task.WhenAll(Bootloader.StopSubFunctions(), CortanaApi.ShutdownService(), stopHardware);
        Console.WriteLine("Termination Sequence Completed!");
    }
}