using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramBot.Modules;
using Processor;
using Telegram.Bot.Types.ReplyMarkups;

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

                if (messageStats.Command == "home") await cortana.SendMessage(messageStats.ChatId, "Home", replyMarkup: CreateMenuButtons());
                else
                {
                    HardwareModule.ExecCommand(messageStats, cortana);
                    UtilityModule.ExecCommand(messageStats, cortana);
                    ShoppingModule.ExecCommand(messageStats, cortana);
                }
            }
            else
            {
                if (UtilityModule.IsWaiting(messageStats.ChatId)) UtilityModule.HandleCallback(messageStats, cortana);
                else if (ShoppingModule.IsWaiting(messageStats.ChatId)) ShoppingModule.HandleCallback(messageStats, cortana);
                else HardwareModule.HandleCallback(messageStats, cortana);
            }
        }


        private static void HandleCallback(ITelegramBotClient cortana, Update update)
        {
            if(update.CallbackQuery == null) return;

            string command = update.CallbackQuery.Data!;

            switch (command)
            {
                case "automation":
                    HardwareModule.CreateAutomationMenu(cortana, update);
                    break;
                case "raspberry":
                    HardwareModule.CreateRaspberryMenu(cortana, update);
                    break;
                case "utility":
                    UtilityModule.CreateUtilityMenu(cortana, update);
                    break;
                default:
                    if(command.StartsWith("hardware-")) HardwareModule.ButtonCallback(cortana, update, command["hardware-".Length..]);
                    else if(command.StartsWith("shopping-")) ShoppingModule.ButtonCallback(cortana, update, command["shopping-".Length..]);
                    else if(command.StartsWith("utility-")) ShoppingModule.ButtonCallback(cortana, update, command["utility-".Length..]);
                    break;
            }
        }
        
        private static InlineKeyboardMarkup CreateMenuButtons()
        {
            var rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Automation", "automation");

            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Raspberry", "raspberry");

            rows[2] = new InlineKeyboardButton[1];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("Utility", "utility");

            return new InlineKeyboardMarkup(rows);
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