namespace Cortana
{
    public class Cortana
    {
        static void Main(string[] args) => new Cortana().BootCortana().GetAwaiter().GetResult();

        async Task<Task> BootCortana()
        {
            SubFunctionsHandler Handler = new SubFunctionsHandler();
            Handler.BootSubFunction(SubFunctionsHandler.ESubFunctions.HardwareDriver);

            int SubFunctionIndex = -1;
            do
            {
                Console.WriteLine("Choose the subordinate function to boot Chief\n0-Cortana API\n1-Discord Bot\n2-Telegram Bot");
                try
                {
                    SubFunctionIndex = Convert.ToInt32(Console.ReadLine());
                }
                catch
                {
                    Console.Clear();
                    continue;
                }

                int ThreadID = -1;
                switch (SubFunctionIndex)
                {
                    case 0:
                        Console.WriteLine("Booting Cortana API subordinate function...");
                        ThreadID = Handler.BootSubFunction(SubFunctionsHandler.ESubFunctions.CortanaAPI);
                        await Task.Delay(1000);
                        Console.WriteLine("Checking API Status");
                        await Task.Delay(500);
                        Console.WriteLine(await RequestsHandler.MakeRequest.Check());
                        await Task.Delay(1000);
                        Console.WriteLine($"Done, Cortana API working on Thread {ThreadID}");
                        break;
                    case 1:
                        Console.WriteLine("Booting Discord Bot subordinate function...");
                        await Task.Delay(500);
                        ThreadID = Handler.BootSubFunction(SubFunctionsHandler.ESubFunctions.DiscordBot);
                        await Task.Delay(500);
                        Console.WriteLine($"Done, DiscordBot working on Thread {ThreadID}");
                        break;
                    case 2:
                        Console.WriteLine("Booting TelegramBot subordinate function...");
                        await Task.Delay(500);
                        ThreadID = Handler.BootSubFunction(SubFunctionsHandler.ESubFunctions.TelegramBot);
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

            return Task.CompletedTask;

        }
    }
}
