using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    static class HardwareEmoji
    {
        public const string LIGHT = "💡";
        public const string PC = "🖥";
        public const string PLUGS = "⚡";
        public const string MONITOR = "📺";
        public const string REBOOT = "🔄";
        public const string ON = "🟩🟩🟩";
        public const string OFF = "🟥🟥🟥";
    }

    public class TelegramBot
    {
        enum EAnswerCommands { QRCODE, CHAT }

        struct AnswerCommand {
            public EAnswerCommands Command;
            public string? CommandValue;
            public AnswerCommand(EAnswerCommands cmd, string? cmdVal = null)
            {
                Command = cmd;
                CommandValue = cmdVal;
            }
        }

        private static Dictionary<long, AnswerCommand> AnswerCommands;
        private static Dictionary<long, string> HardwareAction;
        private static List<long> HardwarePermissions;

        public static void BootTelegramBot() => new TelegramBot().Main();

        public void Main()
        {
            var config = ConfigurationBuilder();
            var cortana = new TelegramBotClient(config["token"]);
            cortana.StartReceiving(UpdateHandler, ErrorHandler);

            TelegramData.Init(cortana);
            AnswerCommands = new();
            HardwareAction = new();
            HardwarePermissions = new()
            {
                TelegramData.NameToID("@gwynn7"),
                TelegramData.NameToID("@alessiaat1")
            };

            TelegramData.SendToUser(TelegramData.NameToID("@gwynn7"), "I'm Online", false);
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
        

        private async void HandleMessage(ITelegramBotClient Cortana, Update update)
        {
            if (update.Message == null) return;

            var ChatID = update.Message.Chat.Id;
            var UserID = update.Message.From == null ? ChatID : update.Message.From.Id;
            if (update.Message.Type == MessageType.Text && update.Message.Text != null)
            {
                if (update.Message.Text.StartsWith("/"))
                {
                    var message = update.Message.Text.Substring(1);
                    var command = message.Split(" ").First();
                    var textList = message.Split(" ").Skip(1).ToList();
                    var text = string.Join(" ", textList);

                    switch (command)
                    {
                        case "ip":
                            var ip = await Utility.Functions.GetPublicIP();
                            await Cortana.SendTextMessageAsync(ChatID, $"IP: {ip}");
                            break;
                        case "temperatura":
                            var temp = Utility.HardwareDriver.GetCPUTemperature();
                            await Cortana.SendTextMessageAsync(ChatID, $"Temperatura: {temp}");
                            break;
                        case "qrcode":
                            if (AnswerCommands.ContainsKey(ChatID)) AnswerCommands.Remove(ChatID);
                            AnswerCommands.Add(ChatID, new AnswerCommand(EAnswerCommands.QRCODE));
                            await Cortana.SendTextMessageAsync(ChatID, "Scrivi il contenuto");
                            break;
                        case "buy":
                            if(Shopping.IsChannelAllowed(ChatID))
                            {
                                bool result = Shopping.Buy(UserID, text);
                                if(result) await Cortana.SendTextMessageAsync(ChatID, "Debiti aggiornati");
                                else await Cortana.SendTextMessageAsync(ChatID, "Non è stato possibile aggiornare i debiti");

                            }
                            else await Cortana.SendTextMessageAsync(ChatID, "Comando riservato al gruppo");
                            break;
                        case "debt":
                            if(Shopping.IsChannelAllowed(ChatID))
                            {
                                if (textList.Count > 0)
                                {
                                    for (int i = 0; i < textList.Count; i++)
                                    {
                                        await Cortana.SendTextMessageAsync(ChatID, Shopping.GetDebts(textList[i]));
                                    }
                                }
                                else await Cortana.SendTextMessageAsync(ChatID, Shopping.GetDebts(TelegramData.IDToName(UserID)));
                            }
                            else await Cortana.SendTextMessageAsync(ChatID, "Comando riservato a specifici gruppi");
                            break;
                        case "hardware":
                            if(HardwarePermissions.Contains(UserID))
                                await Cortana.SendTextMessageAsync(ChatID, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                            else
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");      
                            break;
                        case "keyboard":
                            if (HardwarePermissions.Contains(UserID))
                                await Cortana.SendTextMessageAsync(ChatID, "Hardware Toggle Keyboard", replyMarkup: CreateHardwareToggles());
                            else 
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");         
                            break;
                        case "send":
                            if (HardwarePermissions.Contains(UserID))
                            {
                                if(textList.Count > 1)
                                {
                                    TelegramData.SendToUser(TelegramData.NameToID(textList[0]), string.Join(" ",textList.Skip(1)));
                                    await Cortana.SendTextMessageAsync(ChatID, $"Testo inviato a {textList[0]}"); 
                                }
                                else await Cortana.SendTextMessageAsync(ChatID, "Errore nel numero dei parametri");    
                            }
                            else 
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");         
                            break;
                        case "join":
                            if (HardwarePermissions.Contains(UserID))
                            {
                                if(textList.Count == 1)
                                {
                                    if (AnswerCommands.ContainsKey(ChatID)) AnswerCommands.Remove(ChatID);
                                    AnswerCommands.Add(ChatID, new AnswerCommand(EAnswerCommands.CHAT, textList[0]));
                                    await Cortana.SendTextMessageAsync(ChatID, $"Chat con {textList[0]} avviata"); 
                                }
                                else await Cortana.SendTextMessageAsync(ChatID, "Errore nel numero dei parametri"); 
                            }
                            else 
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");         
                            break;
                        case "leave":
                            if (AnswerCommands.ContainsKey(ChatID) && AnswerCommands[ChatID].Command == EAnswerCommands.CHAT) 
                            {
                                await Cortana.SendTextMessageAsync(ChatID, $"Chat con {AnswerCommands[ChatID].CommandValue} terminata"); 
                                AnswerCommands.Remove(ChatID);
                            }
                            break;
                        case "notify":
                            if (HardwarePermissions.Contains(UserID))
                            {
                                var res = Utility.Functions.NotifyPC(text ?? "Hi, I am Cortana");
                                if(res == "0") await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                else await Cortana.SendTextMessageAsync(ChatID, res);
                            } 
                            else 
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");         
                            break;
                    }
                }
                else
                {
                    if (AnswerCommands.ContainsKey(ChatID))
                    {
                        switch (AnswerCommands[ChatID].Command)
                        {
                            case EAnswerCommands.QRCODE:
                                var ImageStream = Utility.Functions.CreateQRCode(content: update.Message.Text, useNormalColors: false, useBorders: true);
                                ImageStream.Position = 0;
                                await Cortana.SendPhotoAsync(ChatID, new InputFileStream(ImageStream, "QRCODE.png"));
                                AnswerCommands.Remove(ChatID);
                                break;
                            case EAnswerCommands.CHAT:
                                TelegramData.SendToUser(TelegramData.NameToID(AnswerCommands[ChatID].CommandValue!), update.Message.Text);
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        if (!HardwarePermissions.Contains(UserID) || update.Message.Chat.Type != ChatType.Private) return;
                        switch (update.Message.Text)
                        {
                            case HardwareEmoji.LIGHT:
                                Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
                                break;
                            case HardwareEmoji.PC:
                                Utility.HardwareDriver.SwitchPC(EHardwareTrigger.Toggle);
                                break;
                            case HardwareEmoji.PLUGS:
                                Utility.HardwareDriver.SwitchOutlets(EHardwareTrigger.Toggle);
                                break;
                            case HardwareEmoji.ON:
                                Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.On);
                                break;
                            case HardwareEmoji.OFF:
                                Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.Off);
                                break;
                            case HardwareEmoji.REBOOT:
                                Utility.HardwareDriver.RebootPC();
                                break;
                            default:
                                if(UserID == TelegramData.NameToID("@alessiaat1"))
                                    await Cortana.ForwardMessageAsync(TelegramData.NameToID("@gwynn7"), ChatID, update.Message.MessageId);
                                else
                                {
                                    var cmd = "sudo " + update.Message.Text;
                                    var res = Utility.HardwareDriver.SSH_PC(cmd);
                                    string print = res;
                                    if(res == "CONN_ERROR") print = "PC non raggiungibile";
                                    else if(res == "ERROR") print = "Errore esecuzione comando";
                                    if(print == "0") await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                    else await Cortana.SendTextMessageAsync(ChatID, print);
                                }
                                return;
                        }
                        await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                    }
                }
            }
        }


        private async void HandleCallback(ITelegramBotClient Cortana, Update update)
        {
            if (update.CallbackQuery == null || update.CallbackQuery.Data == null || update.CallbackQuery.Message == null) return;
            if (!HardwarePermissions.Contains(update.CallbackQuery.From.Id))
            {
                await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                return;
            }

            string data = update.CallbackQuery.Data;
            int message_id = update.CallbackQuery.Message.MessageId;
            InlineKeyboardMarkup Action;

            if (!HardwareAction.ContainsKey(message_id))
            {
                HardwareAction.Add(message_id, data);
                Action = CreateOnOffButtons();
            }
            else
            {
                if (data != "back")
                {
                    EHardwareTrigger trigger = data switch
                    {
                        "on" => EHardwareTrigger.On,
                        "off" => EHardwareTrigger.Off,
                        "toggle" => EHardwareTrigger.Toggle,
                        _ => EHardwareTrigger.Off
                    };

                    string result = HardwareAction[message_id] switch
                    {
                        "lamp" => Utility.HardwareDriver.SwitchLamp(trigger),
                        "pc" => Utility.HardwareDriver.SwitchPC(trigger),
                        "general" => Utility.HardwareDriver.SwitchGeneral(trigger),
                        "outlets" => Utility.HardwareDriver.SwitchOutlets(trigger),
                        "oled" => Utility.HardwareDriver.SwitchOLED(trigger),
                        "room" => Utility.HardwareDriver.SwitchRoom(trigger),
                        _ => ""
                    };
                }
                HardwareAction.Remove(message_id);
                Action = CreateHardwareButtons();
            }

            await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
            await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, Action);

        }

        private InlineKeyboardMarkup CreateHardwareButtons()
        {
            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[6][];

            Rows[0] = new InlineKeyboardButton[1];
            Rows[0][0] = InlineKeyboardButton.WithCallbackData("Light", "lamp");
         
            Rows[1] = new InlineKeyboardButton[1];
            Rows[1][0] = InlineKeyboardButton.WithCallbackData("PC", "pc");

            Rows[2] = new InlineKeyboardButton[1];
            Rows[2][0] = InlineKeyboardButton.WithCallbackData("General", "general");

            Rows[3] = new InlineKeyboardButton[1];
            Rows[3][0] = InlineKeyboardButton.WithCallbackData("Plugs", "outlets");

            Rows[4] = new InlineKeyboardButton[1];
            Rows[4][0] = InlineKeyboardButton.WithCallbackData("OLED", "oled");

            Rows[5] = new InlineKeyboardButton[1];
            Rows[5][0] = InlineKeyboardButton.WithCallbackData("Room", "room");

            InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(Rows);
            return hardwareKeyboard;
        }

        private InlineKeyboardMarkup CreateOnOffButtons()
        {
            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[3][];

            Rows[0] = new InlineKeyboardButton[2];
            Rows[0][0] = InlineKeyboardButton.WithCallbackData("On", "on");
            Rows[0][1] = InlineKeyboardButton.WithCallbackData("Off", "off");

            Rows[1] = new InlineKeyboardButton[1];
            Rows[1][0] = InlineKeyboardButton.WithCallbackData("Toggle", "toggle");

            Rows[2] = new InlineKeyboardButton[1];
            Rows[2][0] = InlineKeyboardButton.WithCallbackData("<<", "back");
           
            InlineKeyboardMarkup OnOffKeyboard = new InlineKeyboardMarkup(Rows);
            return OnOffKeyboard;
        }

        private ReplyKeyboardMarkup CreateHardwareToggles()
        {
            var Keyboard =
                    new KeyboardButton[][]
                    {
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.LIGHT),
                            new KeyboardButton(HardwareEmoji.PLUGS)
                            
                        },
                        new KeyboardButton[]
                        {

                            new KeyboardButton(HardwareEmoji.PC),
                            new KeyboardButton(HardwareEmoji.REBOOT)

                        },
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.ON),
                        },
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.OFF),
                        },
                        
                    };
            return new ReplyKeyboardMarkup(Keyboard);
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