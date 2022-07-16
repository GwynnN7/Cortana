using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TelegramBot
{
    public class TelegramBot
    {
        public static void BootTelegramBot() => new TelegramBot().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var config = ConfigurationBuilder();
            var botClient = new TelegramBotClient(config["token"]);
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            botClient.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions);
            
            //var me = await botClient.GetMeAsync();
        }

        private async Task UpdateHandler(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if(update.Type == UpdateType.Message)
            {
                if(update.Message?.Type == MessageType.Text)
                {
                    if(update.Message.Text.StartsWith("/"))
                    {
                        var text = "Message: " + update.Message.Text + " from: " + update.Message.From?.Id;
                        var id = update.Message.Chat.Id;

                        var message = update.Message.Text.Substring(1);

                        switch (message)
                        {
                            case "light":
                                Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
                                break;
                            case "ip":
                                var ip = await Utility.Functions.GetPublicIP();
                                await botClient.SendTextMessageAsync(id, text);
                                break;
                        }
                    }
                }
            }
        }

        private Task ErrorHandler(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        private IConfigurationRoot ConfigurationBuilder()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Data/Telegram/Token.json")
                .Build();
        }
    }
}