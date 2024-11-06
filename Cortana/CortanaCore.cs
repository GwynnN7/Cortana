using Utility;

namespace Cortana
{
    using DiscordBot;
    using TelegramBot;
    using CortanaAPI;

    public class CortanaCore
    {
        private readonly Dictionary<ESubFunctions, Task> _subFunctionTasks = new();

        public int BootSubFunction(ESubFunctions subFunction)
        {
            Task subFunctionTask;
            switch (subFunction)
            {
                case ESubFunctions.CortanaApi:
                    subFunctionTask = Task.Run(CortanaApi.BootCortanaApi);
                    break;
                case ESubFunctions.DiscordBot:
                    subFunctionTask = Task.Run(DiscordBot.BootDiscordBot);
                    break;
                case ESubFunctions.TelegramBot:
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
