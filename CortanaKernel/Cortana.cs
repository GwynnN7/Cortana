using CortanaKernel.API;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Structures;
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
        Task apiTask = Task.Run(CortanaApi.RunAsync);
        
        Console.WriteLine("Initiating Bootloader...");

        //Bootloader.BootSubFunction(ESubFunctionType.CortanaWeb);
        Bootloader.BootSubFunction(ESubFunctionType.CortanaTelegram);
        Bootloader.BootSubFunction(ESubFunctionType.CortanaDiscord);

        Console.WriteLine("Boot Completed, I'm Online!");
        
        await WaitForSignal(apiTask);
    }

    private static async Task WaitForSignal(Task taskToWait)
    {
        UnixSignal.WaitAny(Signals, Timeout.Infinite);
        Console.WriteLine("Shutting down...");
        
        await Bootloader.StopSubFunctions();
        await CortanaApi.ShutdownService();
        HardwareApi.ShutdownService();

        await taskToWait;
    }
}