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
        DataHandler.Log("Initiating Kernel...");

        EnvService.Load();
        HardwareApi.InitializeHardware();
        IpcService.Publish(EMessageCategory.Telegram, "Initializing IPC Service");

        Task shutdownTask = Task.Run(WaitForShutdown);

        DataHandler.Log("Initializing API...");
        CortanaApi.Initialize();
        Task apiTask = Task.Run(async () => await CortanaApi.RunAsync());

        DataHandler.Log("Boot Completed, I'm Online!");
        await Task.WhenAll(apiTask, shutdownTask);

        DataHandler.Log("Shutting Down...");
    }

    private static async Task WaitForShutdown()
    {
        await SignalHandler.WaitForInterrupt();

        DataHandler.Log("Initiating Termination Sequence...");
        Task stopHardware = Task.Run(async () =>
        {
            await Task.Delay(500);
            HardwareApi.ShutdownService();
            await IpcService.ShutdownService();
        });
        await Task.WhenAll(CortanaApi.ShutdownService(), stopHardware);
        DataHandler.Log("Termination Sequence Completed!");
    }
}