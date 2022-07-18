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
            Console.WriteLine("Subfunctions Handler Ready");

            Utility.EmailHandler.Init();
            Utility.HardwareDriver.Init();
            Console.WriteLine("Hardware Driver & Email Handler Ready");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.CortanaAPI);
            Console.WriteLine($"Cortana API Ready on Task {ThreadID}");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.DiscordBot);
            Console.WriteLine($"Discord Bot booting on Task {ThreadID}, sending verification now...");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubFunctions.TelegramBot);
            Console.WriteLine($"Telegram Bot booting on Task {ThreadID}, sending verification now...");

            await Task.Delay(500);

            Console.WriteLine("Booting Completed, I'm Ready Chief!");
            Console.Read();

            return Task.CompletedTask;

        }
    }
}
