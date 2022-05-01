using System.Reflection;

namespace Cortana
{
    using DiscordBot;
    using TelegramBot;

    public class CortanaCore
    {
        private Dictionary<ESubFunctions, Thread> SubFunctionThreads;
        private static WebApplication CortanaAPI;

        public CortanaCore()
        {
            
            SubFunctionThreads = new Dictionary<ESubFunctions, Thread>();
        }

        public int BootSubFunction(ESubFunctions SubFunction)
        {
            Thread SubFunctionThread;
            switch (SubFunction)
            {
                case ESubFunctions.CortanaAPI:
                    SubFunctionThread = new Thread(() => BootCortanaAPI());
                    break;
                case ESubFunctions.DiscordBot:
                    SubFunctionThread = new Thread(() => DiscordBot.BootDiscordBot());
                    break;
                case ESubFunctions.TelegramBot:
                    SubFunctionThread = new Thread(() => TelegramBot.BootTelegramBot());
                    break;
                default:
                    return -1;
            }
            SubFunctionThreads.Add(SubFunction, SubFunctionThread);
            SubFunctionThread.Start();
            return SubFunctionThread.ManagedThreadId;
        }

        private static void BootCortanaAPI()
        {
            var builder = WebApplication.CreateBuilder();

            Assembly RequestsHandlerAssemby = Assembly.Load(new AssemblyName("CortanaAPI"));
            builder.Services.AddMvc().AddApplicationPart(RequestsHandlerAssemby);
            builder.Services.AddControllers();

            CortanaAPI = builder.Build();
            CortanaAPI.UsePathBase("/cortana-api");
            CortanaAPI.UseAuthorization();
            CortanaAPI.MapControllers();

            CortanaAPI.Run();
        }

        public async Task StopFunctions()
        {
            await DiscordBot.Disconnect();
            await CortanaAPI.StopAsync();
        }
    }
}
