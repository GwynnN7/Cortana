using Utility;

namespace Cortana
{
    using DiscordBot;
    using TelegramBot;
    using CortanaAPI;

    public class CortanaCore
    {
        private readonly Dictionary<ESubfunctions, Task> _subFunctionTasks = new Dictionary<ESubfunctions, Task>();

        public int BootSubFunction(ESubfunctions subFunction)
        {
            Task subFunctionTask;
            switch (subFunction)
            {
                case ESubfunctions.CortanaApi:
                    subFunctionTask = Task.Run(CortanaApi.BootCortanaApi);
                    break;
                case ESubfunctions.DiscordBot:
                    subFunctionTask = Task.Run(DiscordBot.BootDiscordBot);
                    break;
                case ESubfunctions.TelegramBot:
                    subFunctionTask = Task.Run(TelegramBot.BootTelegramBot);
                    break;
                default:
                    return -1;
            }
            _subFunctionTasks.Add(subFunction, subFunctionTask);
            return subFunctionTask.Id;
        }
    }
}
