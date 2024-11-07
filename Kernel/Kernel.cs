using Processor;

namespace Kernel
{
    public static class Kernel
    {
        private static void Main() => BootCortana().GetAwaiter().GetResult();

        private static async Task<Task> BootCortana()
        {
            Console.Clear();

            Console.WriteLine("Booting up...");

            var handler = new Bootloader();
            Console.WriteLine("Sub-functions Handler is ready");
            
            Console.WriteLine($"Loading data for {Hardware.GetLocation()}");
            await Task.Delay(500);

            int threadId = handler.BootSubFunction(ESubFunctions.CortanaApi);
            Console.WriteLine($"Cortana API ready on Task {threadId}, check on http://cortana-api.ddns.net:8080/");

            await Task.Delay(500);

            threadId = handler.BootSubFunction(ESubFunctions.DiscordBot);
            Console.WriteLine($"Discord Bot booting up on Task {threadId}, wait for a verification on Discord!");

            await Task.Delay(500);

            threadId = handler.BootSubFunction(ESubFunctions.TelegramBot);
            Console.WriteLine($"Telegram Bot booting up on Task {threadId},  wait for a verification on Telegram!");

            await Task.Delay(500);

            Console.WriteLine("Boot Completed, I'm Online!");
            
            await Task.Delay(Timeout.Infinite);

            return Task.CompletedTask;

        }
    }
}
