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

            ThreadID = Handler.BootSubFunction(ESubfunctions.CortanaAPI);
            Console.WriteLine($"Cortana API ready on Task {ThreadID}, check on http://cortana-api.ddns.net:8080/");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubfunctions.DiscordBot);
            Console.WriteLine($"Discord Bot booting up on Task {ThreadID}, wait for a verification on Discord!");

            await Task.Delay(500);

            ThreadID = Handler.BootSubFunction(ESubfunctions.TelegramBot);
            Console.WriteLine($"Telegram Bot booting up on Task {ThreadID},  wait for a verification on Telegram!");

            await Task.Delay(500);

            Console.WriteLine("Boot Completed, I'm Online!");
            
            await Task.Delay(Timeout.Infinite);

            return Task.CompletedTask;

        }
    }
}
