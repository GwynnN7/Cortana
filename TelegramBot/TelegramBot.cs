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
            if(update.Type == UpdateType.CallbackQuery)
            {
                string data = update.CallbackQuery.Data;
                string type = data.Split("-").First();
                string action = data.Split("-").Last();

                EHardwareTrigger state = action == "on" ? EHardwareTrigger.On : EHardwareTrigger.Off;


                string result = type switch
                {
                    "lamp" => Utility.HardwareDriver.SwitchLamp(state),
                    "pc" => Utility.HardwareDriver.SwitchPC(state),
                    "outlets" => Utility.HardwareDriver.SwitchOutlets(state),
                    "oled" => Utility.HardwareDriver.SwitchOLED(state),
                    "led" => Utility.HardwareDriver.SwitchLED(state),
                    //"room" => Utility.HardwareDriver.SwitchRoom(state),
                    _ => ""
                };
                if(type == "fan")
                {
                    EFanSpeeds fanSpeed = action switch
                    {
                        "off" => EFanSpeeds.Off,
                        "low" => EFanSpeeds.Low,
                        "medium" => EFanSpeeds.Medium,
                        "high" => EFanSpeeds.High,
                        _ => EFanSpeeds.Off
                    };
                    result = Utility.HardwareDriver.SetFanSpeed(fanSpeed);
                }

                await botClient.AnswerCallbackQueryAsync(update.CallbackQuery.Id, result);
            }
            else if (update.Type == UpdateType.Message)
            {
                if(update.Message?.Type == MessageType.Text)
                {
                    if(update.Message.Text.StartsWith("/"))
                    {
                        var id = update.Message.Chat.Id;
                        var message = update.Message.Text.Substring(1);

                        switch (message)
                        {
                            case "ip":
                                var ip = await Utility.Functions.GetPublicIP();
                                await botClient.SendTextMessageAsync(id, $"IP: {ip}");
                                break;
                            case "hardware":
                                {
                                    InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(

                                        new InlineKeyboardButton[][]
                                        {
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Lamp On",
                                                    "lamp-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Lamp Off",
                                                    "lamp-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "PC On",
                                                    "pc-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "PC Off",
                                                    "pc-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Outlets On",
                                                    "outlets-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Outlets Off",
                                                    "outlets-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "OLED On",
                                                    "oled-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "OLED Off",
                                                    "oled-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "LED On",
                                                    "led-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "LED Off",
                                                    "led-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Room On",
                                                    "room-on"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Room Off",
                                                    "room-off"
                                                )
                                            },
                                            new InlineKeyboardButton[]
                                            {
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Fan Off",
                                                    "fan-off"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Fan Low",
                                                    "fan-low"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Fan Medium",
                                                    "fan-medium"
                                                ),
                                                InlineKeyboardButton.WithCallbackData(
                                                    "Fan High",
                                                    "fan-high"
                                                )
                                            }
                                        }
                                    );
                                    await botClient.SendTextMessageAsync(id, "Hardware", replyMarkup: hardwareKeyboard);
                                    break;
                                }
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