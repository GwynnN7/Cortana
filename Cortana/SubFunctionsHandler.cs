using System.Reflection;

namespace Cortana
{
    using DiscordBot;
    using TelegramBot;

    public class SubFunctionsHandler
    {
        public enum ESubFunctions
        {
            CortanaAPI,
            DiscordBot,
            TelegramBot,
            RequestsHandler,
            HardwareDriver
        }

        private Dictionary<ESubFunctions, Thread> SubFunctionThreads;

        public SubFunctionsHandler()
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
                case ESubFunctions.HardwareDriver:
                    HardwareDriver.Driver.BlinkLED();
                    return -1;
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

            Assembly RequestsHandlerAssemby = Assembly.Load(new AssemblyName("RequestsHandler"));
            builder.Services.AddMvc().AddApplicationPart(RequestsHandlerAssemby);
            builder.Services.AddControllers();

            var app = builder.Build();
            app.UsePathBase("/cortana-api");
            app.UseAuthorization();
            app.MapControllers();
    
            app.Run();
        }
    }
}
