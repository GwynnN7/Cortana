namespace Cortana
{
    public class Cortana
    {
        static void Main(string[] args) => new Cortana().BootCortana().GetAwaiter().GetResult();

        async Task<Task> BootCortana()
        {
            Console.Clear();

            Console.WriteLine("Booting up...");

            int ThreadID;
            CortanaCore Handler = new CortanaCore();
            Console.WriteLine("Subfunctions Handler is ready");

            Utility.HardwareDriver.Init();
            Console.WriteLine("Hardware Driver is ready");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.CortanaAPI);
            Console.WriteLine($"Cortana API ready on Task {ThreadID}");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.DiscordBot);
            Console.WriteLine($"Discord Bot booting up on Task {ThreadID}, wait for a verification on Discord!");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.TelegramBot);
            Console.WriteLine($"Telegram Bot booting up on Task {ThreadID},  wait for a verification on Telegram!");

            await Task.Delay(500);

            Console.WriteLine("Booting Completed, I'm ready Chief!");
            Console.Read();

            return Task.CompletedTask;

        }
    }
}
