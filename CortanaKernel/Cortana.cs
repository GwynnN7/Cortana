using CortanaKernel.API;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;
using dotenv.net;
using Iot.Device.Display;
using Mono.Unix;
using Mono.Unix.Native;

namespace CortanaKernel;

public static class Cortana
{
    private static async Task Main()
    {
        Console.Clear();
		
        Console.WriteLine("Build completed");
        
        DotEnv.Load();
        
        Console.WriteLine("Initiating Hardware...");
        Task shutdownTask = Task.Run(WaitForShutdown);
        StringResult temp = HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.Temperature);
        StringResult location = HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.Location);
        
        if (!temp.IsOk || !location.IsOk)
        {
            Console.WriteLine("Failed to initialize hardware, quitting...");
            return;
        }
        Console.WriteLine($"CPU Temperature: {temp.Value}, loaded data for {location.Value}");

        Console.WriteLine("Initializing API...");
        Task apiTask = Task.Run(async () => await CortanaApi.RunAsync());
        
        Console.WriteLine("Loading Bootloader...");
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaTelegram, ESubfunctionAction.Build);
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaDiscord, ESubfunctionAction.Build);
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaWeb, ESubfunctionAction.Build);
        
        Console.WriteLine("Boot Completed, I'm Online!");
        await Task.WhenAll(apiTask, shutdownTask);
        
        Console.WriteLine("Shutting Down...");
    }

    private static async Task WaitForShutdown()
    {
        await Signals.WaitForInterrupt();
        
        Console.WriteLine("Initiating Termination Sequence...");
        Task stopHardware = Task.Run(async () =>
        {
            await Task.Delay(500);
            IpcService.ShutdownService();
            HardwareApi.ShutdownService();
        });
        await Task.WhenAll(Bootloader.StopSubfunctions(), CortanaApi.ShutdownService(), stopHardware);
        Console.WriteLine("Termination Sequence Completed!");
    }
}