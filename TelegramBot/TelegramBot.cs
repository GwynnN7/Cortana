using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    public class TelegramBot
    {
        public static void BootTelegramBot() => new TelegramBot().Main();

        public void Main()
        {
            var config = ConfigurationBuilder();
            var cortana = new TelegramBotClient(config["token"]);
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };
            cortana.StartReceiving(UpdateHandler, ErrorHandler, receiverOptions);

            TelegramData.Init(cortana);
        }

        private Task UpdateHandler(ITelegramBotClient Cortana, Update update, CancellationToken cancellationToken)
        {
            switch (update.Type)
            {
                case UpdateType.CallbackQuery:
                    HandleCallback(Cortana, update);
                    break;
                case UpdateType.Message:
                    HandleMessage(Cortana, update);
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }

        private async void HandleCallback(ITelegramBotClient Cortana, Update update)
        {
            if (update.CallbackQuery == null || update.CallbackQuery.Data == null) return;

            string data = update.CallbackQuery.Data;

            string result = data switch
            {
                "lamp" => Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle),
                "pc" => Utility.HardwareDriver.SwitchPC(EHardwareTrigger.Toggle),
                "outlets" => Utility.HardwareDriver.SwitchOutlets(EHardwareTrigger.Toggle),
                "oled" => Utility.HardwareDriver.SwitchOLED(EHardwareTrigger.Toggle),
                "led" => Utility.HardwareDriver.SwitchLED(EHardwareTrigger.Toggle),
                "fan" => Utility.HardwareDriver.SwitchFan(EHardwareTrigger.Toggle),
                "room" => Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.Toggle),
                _ => ""
            };
            await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id, result);
        }

        private async void HandleMessage(ITelegramBotClient Cortana, Update update)
        {
            if (update.Message == null) return;

            var ChatID = update.Message.Chat.Id;
            if (update.Message.Type == MessageType.Text && update.Message.Text != null)
            {
                if (update.Message.Text.StartsWith("/"))
                {
                    var message = update.Message.Text.Substring(1).Split(" ").First();

                    switch (message)
                    {
                        case "ip":
                            var ip = await Utility.Functions.GetPublicIP();
                            await Cortana.SendTextMessageAsync(ChatID, $"IP: {ip}");
                            break;
                        case "hardware":
                            await Cortana.SendTextMessageAsync(ChatID, "Gestisci il tuo hardware", replyMarkup: CreateHardwareButtons());
                            break;
                    }
                }
            }
        }

        private InlineKeyboardMarkup CreateHardwareButtons()
        {
            Dictionary<string, string> HardwareElements = new Dictionary<string, string>()
            {
                { "Lamp", "lamp" },
                { "PC", "pc" },
                { "Fan", "fan" },
                { "Plugs", "outlets" },
                { "OLED", "oled" },
                { "LED", "led" },
                { "Room", "room" }
            };


            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[7][];

            for (int i = 0; i <= 6; i += 2)
            {
                int len = i == 6 ? 1 : 2;
                var currentLine = new InlineKeyboardButton[len];

                for (int j = 0; j < len; j++)
                {
                    currentLine[j] = InlineKeyboardButton.WithCallbackData(HardwareElements.Keys.ToArray()[i + j], HardwareElements.Values.ToArray()[i + j]);
                }

                Rows[i] = currentLine;
            }

            InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(Rows);
            return hardwareKeyboard;
        }

        private Task ErrorHandler(ITelegramBotClient Cortana, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            TelegramData.SendToUser(TelegramData.ChiefID, ErrorMessage);
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