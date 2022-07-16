namespace Cortana
{
    public class Cortana
    {
        static void Main(string[] args) => new Cortana().BootCortana().GetAwaiter().GetResult();

        async Task<Task> BootCortana()
        {
            Console.Clear();

            Console.WriteLine("Booting up...");
            CortanaCore Handler = new CortanaCore();
            await Task.Delay(500);

            Console.WriteLine("Booting Email handler...");
            Utility.EmailHandler.Init();
            Console.WriteLine("Done");
            await Task.Delay(500);

            Console.WriteLine("Starting Night Handler...");
            Utility.HardwareDriver.HandleNight();
            Console.WriteLine("Done");
            await Task.Delay(500);

            int ThreadID;

            Console.WriteLine("Booting Cortana API subordinate function...");
            ThreadID = Handler.BootSubFunction(ESubFunctions.CortanaAPI);
            await Task.Delay(1000);
            Console.WriteLine($"Done, Cortana API working on Task {ThreadID}");

            await Task.Delay(500);

            Console.WriteLine("Booting Discord Bot subordinate function...");
            ThreadID = Handler.BootSubFunction(ESubFunctions.DiscordBot);
            await Task.Delay(1000);
            Console.WriteLine($"Done, DiscordBot working on Task {ThreadID}");

            await Task.Delay(500);

            Console.WriteLine("Booting Telegram Bot subordinate function...");
            ThreadID = Handler.BootSubFunction(ESubFunctions.TelegramBot);
            await Task.Delay(1000);
            Console.WriteLine($"Done, TelegramBot working on Task {ThreadID}");

            Console.Read();

            Console.WriteLine("Shutting Down");
            await Handler.StopFunctions();

            return Task.CompletedTask;

        }
    }
}
