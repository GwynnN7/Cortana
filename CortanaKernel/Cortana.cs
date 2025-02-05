using CortanaKernel.API;
using CortanaKernel.Hardware;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel;

public static class Cortana
{
    private static async Task Main()
    {
        DataHandler.Log(nameof(CortanaKernel), "Initiating Kernel...");
        
        EnvService.Load();
        Bootloader.LoadKernel();
        Task shutdownTask = Task.Run(WaitForShutdown);

        DataHandler.Log(nameof(CortanaKernel), "Initializing API...");
        Task apiTask = Task.Run(async () => await CortanaApi.RunAsync());
        
        DataHandler.Log(nameof(CortanaKernel), "Loading Bootloader...");
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaTelegram, ESubfunctionAction.Reboot);
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaDiscord, ESubfunctionAction.Reboot);
        await Bootloader.SubfunctionCall(ESubFunctionType.CortanaWeb, ESubfunctionAction.Reboot);
        
        DataHandler.Log(nameof(CortanaKernel), "Boot Completed, I'm Online!");
        await Task.WhenAll(apiTask, shutdownTask);
        
        DataHandler.Log(nameof(CortanaKernel), "Shutting Down...");
    }

    private static async Task WaitForShutdown()
    {
        await SignalHandler.WaitForInterrupt();
        
        Console.WriteLine("Initiating Termination Sequence...");
        Task stopHardware = Task.Run(async () =>
        {
            await Task.Delay(500);
            HardwareApi.ShutdownService();
            await IpcService.ShutdownService();
        });
        await Task.WhenAll(Bootloader.StopSubfunctions(), CortanaApi.ShutdownService(), stopHardware);
        Console.WriteLine("Termination Sequence Completed!");
    }
}