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
        private static Dictionary<long, string> HardwareAction;

        public static void BootTelegramBot() => new TelegramBot().Main();

        public void Main()
        {
            var config = ConfigurationBuilder();
            var cortana = new TelegramBotClient(config["token"]);
            cortana.StartReceiving(UpdateHandler, ErrorHandler);

            TelegramData.Init(cortana);
            HardwareAction = new();

            TelegramData.SendToUser(TelegramData.ChiefID, "I'm Ready Chief!");
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
            if (update.CallbackQuery == null || update.CallbackQuery.Data == null || update.CallbackQuery.Message == null) return;
          
            string data = update.CallbackQuery.Data;
            int message_id = update.CallbackQuery.Message.MessageId;

            if (HardwareAction.ContainsKey(message_id))
            {
                if (HardwareAction[message_id] == "")
                {
                    HardwareAction[message_id] = data;

                    InlineKeyboardMarkup Action = data != "fan" ? CreateOnOffButtons() : CreateSpeedButtons();
                    await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, Action);
                }
                else
                {
                    if(data == "back")
                    {
                        HardwareAction[message_id] = "";
                        await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, CreateHardwareButtons());
                        return;
                    }

                    string result;
                    if(HardwareAction[message_id] == "fan")
                    {
                        EFanSpeeds speeds = data switch
                        {
                            "0" => EFanSpeeds.Off,
                            "1" => EFanSpeeds.Low,
                            "2" => EFanSpeeds.Medium,
                            "3" => EFanSpeeds.High,
                            _ => EFanSpeeds.Off
                        };
                        result = Utility.HardwareDriver.SetFanSpeed(speeds);
                    }
                    else
                    {
                        EHardwareTrigger trigger = data switch
                        {
                            "on" => EHardwareTrigger.On,
                            "off" => EHardwareTrigger.Off,
                            "toggle" => EHardwareTrigger.Toggle,
                            _ => EHardwareTrigger.Off
                        };
                        result = HardwareAction[message_id] switch
                        {
                            "lamp" => Utility.HardwareDriver.SwitchLamp(trigger),
                            "pc" => Utility.HardwareDriver.SwitchPC(trigger),
                            "outlets" => Utility.HardwareDriver.SwitchOutlets(trigger),
                            "oled" => Utility.HardwareDriver.SwitchOLED(trigger),
                            "led" => Utility.HardwareDriver.SwitchLED(trigger),
                            "room" => Utility.HardwareDriver.SwitchRoom(trigger),
                            _ => ""
                        };
                    }

                    HardwareAction[message_id] = "";
                    await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id, result);
                    await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, CreateHardwareButtons());
                }
            }
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
                        case "temperatura":
                            var temp = Utility.HardwareDriver.GetCPUTemperature();
                            await Cortana.SendTextMessageAsync(ChatID, $"Temperatura: {temp}");
                            break;
                        case "hardware":
                            var mex = await Cortana.SendTextMessageAsync(ChatID, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                            HardwareAction.Add(mex.MessageId, "");
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


            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[4][];
            int elementIndex = 0;
            for (int i = 0; i < 4; i ++)
            {
                int len = i == 3 ? 1 : 2;
                var currentLine = new InlineKeyboardButton[len];

                for (int j = 0; j < len; j++)
                {
                    currentLine[j] = InlineKeyboardButton.WithCallbackData(HardwareElements.Keys.ToArray()[elementIndex], HardwareElements.Values.ToArray()[elementIndex]);
                    elementIndex++;
                }
                Rows[i] = currentLine;
            }

            InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(Rows);
            return hardwareKeyboard;
        }

        private InlineKeyboardMarkup CreateOnOffButtons()
        {
            Dictionary<string, string> OnOffElements = new Dictionary<string, string>()
            {
                { "On", "on" },
                { "Off", "off" },
                { "Toggle", "toggle" },
                { "<<", "back" }
            };

            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[3][];
            int elementIndex = 0;
            for (int i = 0; i < 3; i ++)
            {
                int len = i == 0 ? 2 : 1;
                var currentLine = new InlineKeyboardButton[len];

                for (int j = 0; j < len; j++)
                {
                    currentLine[j] = InlineKeyboardButton.WithCallbackData(OnOffElements.Keys.ToArray()[elementIndex], OnOffElements.Values.ToArray()[elementIndex]);
                    elementIndex++;
                }
                Rows[i] = currentLine;
            }

            InlineKeyboardMarkup OnOffKeyboard = new InlineKeyboardMarkup(Rows);
            return OnOffKeyboard;
        }

        private InlineKeyboardMarkup CreateSpeedButtons()
        {
            Dictionary<string, string> SpeedElements = new Dictionary<string, string>()
            {
                { "0", "0" },
                { "1", "1" },
                { "2", "2" },
                { "3", "3" },
                { "<<", "back" }
            };

            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[2][];
            int elementIndex = 0;
            for (int i = 0; i < 2; i ++)
            {
                int len = i == 0 ? 4 : 1;
                var currentLine = new InlineKeyboardButton[len];

                for (int j = 0; j < len; j++)
                {
                    currentLine[j] = InlineKeyboardButton.WithCallbackData(SpeedElements.Keys.ToArray()[elementIndex], SpeedElements.Values.ToArray()[elementIndex]);
                    elementIndex++;
                }
                Rows[i] = currentLine;
            }

            InlineKeyboardMarkup SpeedKeyboard = new InlineKeyboardMarkup(Rows);
            return SpeedKeyboard;
        }

        private Task ErrorHandler(ITelegramBotClient Cortana, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            string path = "Telegram Log.txt";
            using StreamWriter logFile = System.IO.File.Exists(path) ? System.IO.File.AppendText(path) : System.IO.File.CreateText(path);
            logFile.WriteLine($"{DateTime.Now} Exception: " + ErrorMessage);

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