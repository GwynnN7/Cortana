namespace Cortana
{
    using DiscordBot;
    using TelegramBot;
    using CortanaAPI;

    public class CortanaCore
    {
        private Dictionary<ESubFunctions, Thread> SubFunctionThreads;

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
                    SubFunctionThread = new Thread(() => CortanaAPI.BootCortanaAPI());
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

        public async Task StopFunctions()
        {
            await DiscordBot.Disconnect();
            await CortanaAPI.Disconnect();
        }
    }
}
