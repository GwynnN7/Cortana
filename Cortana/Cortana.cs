namespace Cortana
{
    public class Cortana
    {
        static void Main(string[] args) => new Cortana().BootCortana().GetAwaiter().GetResult();

        async Task<Task> BootCortana()
        {
            Console.Clear();

            CortanaCore Handler = new CortanaCore();
            Utility.EmailHandler.Init();

            int SubFunctionIndex = -1;
            do
            {
                Console.Clear();
                Console.WriteLine("Choose the subordinate function to boot Chief\n0-Cortana API\n1-Discord Bot\n2-Telegram Bot");

                try {SubFunctionIndex = Convert.ToInt32(Console.ReadLine());}
                catch {continue;}

                int ThreadID;
                switch (SubFunctionIndex)
                {
                    case 0:
                        Console.WriteLine("Booting Cortana API subordinate function...");
                        ThreadID = Handler.BootSubFunction(ESubFunctions.CortanaAPI);
                        await Task.Delay(1000);
                        Console.WriteLine($"Done, Cortana API working on Thread {ThreadID}");
                        break;
                    case 1:
                        Console.WriteLine("Booting Discord Bot subordinate function...");
                        ThreadID = Handler.BootSubFunction(ESubFunctions.DiscordBot);
                        await Task.Delay(1000);
                        Console.WriteLine($"Done, DiscordBot working on Thread {ThreadID}");
                        break;
                    case 2:
                        Console.WriteLine("Booting TelegramBot subordinate function...");
                        await Task.Delay(1000);
                        ThreadID = Handler.BootSubFunction(ESubFunctions.TelegramBot);
                        break;
                    case -1:
                        Console.WriteLine("Shutting Down");
                        break;
                    default:
                        Console.WriteLine("Unknown subordinate function Chief");
                        break; 
                }
                await Task.Delay(500);
                Console.Clear();

            } while (SubFunctionIndex != -1);

            await Handler.StopFunctions();
            
            return Task.CompletedTask;

        }
    }
}
