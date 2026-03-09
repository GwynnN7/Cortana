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
        HardwareApi.InitializeHardware();
        IpcService.Publish(EMessageCategory.Urgent, "Initializing IPC Service");

        Task shutdownTask = Task.Run(WaitForShutdown);

        DataHandler.Log(nameof(CortanaKernel), "Initializing API...");
        CortanaApi.Initialize();
        Task apiTask = Task.Run(async () => await CortanaApi.RunAsync());

        DataHandler.Log(nameof(CortanaKernel), "Boot Completed, I'm Online!");
        await Task.WhenAll(apiTask, shutdownTask);

        DataHandler.Log(nameof(CortanaKernel), "Shutting Down...");
    }

    private static async Task WaitForShutdown()
    {
        await SignalHandler.WaitForInterrupt();

        DataHandler.Log(nameof(CortanaKernel), "Initiating Termination Sequence...");
        Task stopHardware = Task.Run(async () =>
        {
            await Task.Delay(500);
            HardwareApi.ShutdownService();
            await IpcService.ShutdownService();
        });
        await Task.WhenAll(CortanaApi.ShutdownService(), stopHardware);
        DataHandler.Log(nameof(CortanaKernel), "Termination Sequence Completed!");
    }
}