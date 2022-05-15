namespace Cortana
{
    using DiscordBot;
    using TelegramBot;
    using CortanaAPI;

    public class CortanaCore
    {
        private Dictionary<ESubFunctions, Task> SubFunctionTasks;

        public CortanaCore()
        {
            
            SubFunctionTasks = new Dictionary<ESubFunctions, Task>();
        }

        public int BootSubFunction(ESubFunctions SubFunction)
        {
            Task SubFunctionTask;
            switch (SubFunction)
            {
                case ESubFunctions.CortanaAPI:
                    SubFunctionTask = Task.Run(() => CortanaAPI.BootCortanaAPI());
                    break;
                case ESubFunctions.DiscordBot:
                    SubFunctionTask = Task.Run(() => DiscordBot.BootDiscordBot());
                    break;
                case ESubFunctions.TelegramBot:
                    SubFunctionTask = Task.Run(() => TelegramBot.BootTelegramBot());
                    break;
                default:
                    return -1;
            }
            SubFunctionTasks.Add(SubFunction, SubFunctionTask);
            return SubFunctionTask.Id;
        }

        public async Task StopFunctions()
        {
            await DiscordBot.Disconnect();
            await CortanaAPI.Disconnect();
        }
    }
}
