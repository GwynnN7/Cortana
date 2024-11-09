using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Modules;
using Processor;

namespace TelegramBot
{
    public static class TelegramBot
    {
        public static void BootTelegramBot() => Main();

        private static void Main()
        {
            var cortana = new TelegramBotClient(Software.Secrets.TelegramToken);
            cortana.StartReceiving(UpdateHandler, ErrorHandler);
            
            TelegramUtils.Init(cortana);
            TelegramUtils.SendToUser(TelegramUtils.NameToId("@gwynn7"), "I'm Online", false);
        }

        private static Task UpdateHandler(ITelegramBotClient cortana, Update update, CancellationToken cancellationToken)
        {
            switch (update.Type)
            {
                case UpdateType.CallbackQuery:
                    HandleCallback(cortana, update);
                    break;
                case UpdateType.Message:
                    HandleMessage(cortana, update);
                    break;
                default:
                    return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        private static async void HandleMessage(ITelegramBotClient cortana, Update update)
        {
            if (update.Message == null) return;
            if (update.Message.Type != MessageType.Text || update.Message.Text == null) return;
            if (update.Message.From == null || update.Message.From.IsBot) return;
            
            var messageStats = new MessageStats
            {
                ChatId = update.Message.Chat.Id,
                UserId = update.Message.From?.Id ?? update.Message.Chat.Id,
                MessageId = update.Message.MessageId,
                ChatType = update.Message.Chat.Type,
                FullMessage = update.Message.Text
            };

            if (messageStats.UserId != TelegramUtils.NameToId("@gwynn7") && messageStats.ChatType == ChatType.Private) await cortana.ForwardMessage(TelegramUtils.NameToId("@gwynn7"), messageStats.ChatId, messageStats.MessageId);
            
            if (update.Message.Text.StartsWith('/'))
            {
                messageStats.FullMessage = messageStats.FullMessage[1..];
                messageStats.Command = messageStats.FullMessage.Split(" ").First().Replace("@CortanaAIBot", "");
                messageStats.TextList = messageStats.FullMessage.Split(" ").Skip(1).ToList();
                messageStats.Text = string.Join(" ", messageStats.TextList);

                HardwareModule.ExecCommand(messageStats, cortana);
                UtilityModule.ExecCommand(messageStats, cortana);
                ShoppingModule.ExecCommand(messageStats, cortana);
            }
            else
            {
                if (UtilityModule.IsWaiting(messageStats.ChatId))
                {
                    UtilityModule.HandleCallback(messageStats, cortana);
                    return;
                }
                if (ShoppingModule.IsWaiting(messageStats.ChatId))
                {
                    ShoppingModule.HandleCallback(messageStats, cortana);
                    return;
                }
                HardwareModule.HandleCallback(messageStats, cortana);
            }
        }


        private static void HandleCallback(ITelegramBotClient cortana, Update update)
        {
            if (update.CallbackQuery?.Data == null || update.CallbackQuery.Message == null) return;
            
            HardwareModule.ButtonCallback(cortana, update);
            ShoppingModule.ButtonCallback(cortana, update);
        }

        private static Task ErrorHandler(ITelegramBotClient cortana, Exception exception, CancellationToken cancellationToken)
        {
            string errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            Software.Log("Telegram", errorMessage);
            return Task.CompletedTask;
        }
    }
}