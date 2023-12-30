namespace Cortana
{
    using DiscordBot;
    using TelegramBot;
    using CortanaAPI;

    public class CortanaCore
    {
        private Dictionary<ESubfunctions, Task> SubFunctionTasks;

        public CortanaCore()
        {
            SubFunctionTasks = new Dictionary<ESubfunctions, Task>();
        }

        public int BootSubFunction(ESubfunctions SubFunction)
        {
            Task SubfunctionTask;
            switch (SubFunction)
            {
                case ESubfunctions.CortanaAPI:
                    SubfunctionTask = Task.Run(() => CortanaAPI.BootCortanaAPI());
                    break;
                case ESubfunctions.DiscordBot:
                    SubfunctionTask = Task.Run(() => DiscordBot.BootDiscordBot());
                    break;
                case ESubfunctions.TelegramBot:
                    SubfunctionTask = Task.Run(() => TelegramBot.BootTelegramBot());
                    break;
                default:
                    return -1;
            }
            SubFunctionTasks.Add(SubFunction, SubfunctionTask);
            return SubfunctionTask.Id;
        }
    }
}
