using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Processor;

namespace TelegramBot.Modules
{
    internal static class HardwareEmoji
    {
        public const string Bulb = "💡";
        public const string Pc = "🖥";
        public const string Thunder = "⚡";
        public const string Reboot = "🔄";
        public const string On = "\ud83c\udf15\ud83c\udf15\ud83c\udf15";
        public const string Off = "\ud83c\udf11\ud83c\udf11\ud83c\udf11";
    }
    
    public static class HardwareModule
    {
        private static readonly Dictionary<long, string> HardwareAction = new();
        
        public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "domotica":
                    if (TelegramUtils.CheckPermission(messageStats.UserId))
                        await cortana.SendMessage(messageStats.ChatId, "Keyboard Domotica", replyMarkup: CreateHardwareToggles());
                    else await cortana.SendMessage(messageStats.ChatId, "Sorry, you can't use this command");
                    break;
                case "ssh":
                    if (TelegramUtils.CheckPermission(messageStats.UserId))
                    {
                        Hardware.SendCommand(messageStats.Text, asRoot: true, result: out string result);
                        await cortana.SendMessage(messageStats.ChatId, result);
                    }
                    else await cortana.SendMessage(messageStats.ChatId, "Sorry, you can't use this command");
                    break;
            }
        }
        
        public static async void CreateAutomationMenu(ITelegramBotClient cortana, Update update)
        {
            Message message = update.CallbackQuery!.Message!;
            HardwareAction.Remove(message.Id);
            
            if (TelegramUtils.CheckPermission(update.CallbackQuery.From.Id))
                await cortana.EditMessageText(message.Chat.Id, message.Id, "Hardware Keyboard", replyMarkup: CreateAutomationButtons());
            else
                await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, "Sorry, you can't use this command");
        }
        
        public static async void CreateRaspberryMenu(ITelegramBotClient cortana, Update update)
        {
            Message message = update.CallbackQuery!.Message!;
            
            if (TelegramUtils.CheckPermission(update.CallbackQuery.From.Id))
                await cortana.EditMessageText(message.Chat.Id, message.Id, "Raspberry Handler", replyMarkup: CreateRaspberryButtons());
            else
                await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, "Sorry, you can't access raspberry's controls");
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if (!TelegramUtils.CheckPermission(messageStats.UserId) || messageStats.ChatType != ChatType.Private) return;
            switch (messageStats.FullMessage)
            {
                case HardwareEmoji.Bulb:
                    Hardware.PowerLamp(ETrigger.Toggle);
                    break;
                case HardwareEmoji.Pc:
                    Hardware.PowerComputer(ETrigger.Toggle);
                    break;
                case HardwareEmoji.Thunder:
                    Hardware.PowerGeneric(ETrigger.Toggle);
                    break;
                case HardwareEmoji.On:
                    Hardware.HandleRoom(ETrigger.On);
                    break;
                case HardwareEmoji.Off:
                    Hardware.HandleRoom(ETrigger.Off);
                    break;
                case HardwareEmoji.Reboot:
                    Hardware.CommandPc(EComputerCommand.Reboot);
                    break;
                default:
                    return;
            }
            await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update, string command)
        {
            if(update.CallbackQuery == null) return;
            
            int messageId = update.CallbackQuery.Message!.MessageId;

            if (command.StartsWith("raspberry-"))
            {
                switch (command["raspberry-".Length..])
                {
                    case "ip":
                        string ip = await Hardware.GetPublicIp();
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, $"IP: {ip}");
                        break;
                    case "gateway":
                        string gateway = Hardware.GetDefaultGateway();
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id,  $"Gateway: {gateway}");
                        break;
                    case "location":
                        string location = Hardware.GetLocation();
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id,  $"Location: {location}");
                        break;
                    case "temperature":
                        string temp = Hardware.GetCpuTemperature();
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id,  $"Temperature: {temp}");
                        break;
                    case "reboot":
                        string rebootResult = Hardware.PowerRaspberry(EPowerOption.Reboot);
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id,  rebootResult, true);
                        break;
                    case "shutdown":
                        string shutdownResult = Hardware.PowerRaspberry(EPowerOption.Shutdown);
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id,  shutdownResult, true);
                        break;
                }
            }
            else if (command.StartsWith("automation-"))
            {
                command = command["automation-".Length..];

                if (!HardwareAction.TryAdd(messageId, command))
                {
                    string result = Hardware.SwitchFromString(HardwareAction[messageId], command);
                    await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, result);
                    return;
                }
                await cortana.EditMessageReplyMarkup(update.CallbackQuery.Message.Chat.Id, messageId, replyMarkup: CreateOnOffButtons());
            }
        }
        
        private static InlineKeyboardMarkup CreateAutomationButtons()
        {
            var rows = new InlineKeyboardButton[Enum.GetValues(typeof(EGpio)).Length + 2][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Room", "hardware-automation-room");

            var index = 1;
            foreach (string element in Enum.GetNames(typeof(EGpio)))
            {
                rows[index] = new InlineKeyboardButton[1];
                rows[index][0] = InlineKeyboardButton.WithCallbackData(element, $"hardware-automation-{element.ToLower()}");
                index++;
            }
            
            rows[index] = new InlineKeyboardButton[1];
            rows[index][0] = InlineKeyboardButton.WithCallbackData("<<", "home");

            return new InlineKeyboardMarkup(rows);
        }
        
        private static InlineKeyboardMarkup CreateRaspberryButtons()
        {
            var rows = new InlineKeyboardButton[4][];

            rows[0] = new InlineKeyboardButton[2];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Shutdown", "hardware-raspberry-shutdown");
            rows[0][1] = InlineKeyboardButton.WithCallbackData("Reboot", "hardware-raspberry-reboot");

            rows[1] = new InlineKeyboardButton[2];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Temperature", "hardware-raspberry-temperature");
            rows[1][1] = InlineKeyboardButton.WithCallbackData("IP", "hardware-raspberry-ip");

            rows[2] = new InlineKeyboardButton[2];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("Location", "hardware-raspberry-location");
            rows[2][1] = InlineKeyboardButton.WithCallbackData("Gateway", "hardware-raspberry-gateway");
            
            rows[3] = new InlineKeyboardButton[1];
            rows[3][0] = InlineKeyboardButton.WithCallbackData("<<", "home");

            return new InlineKeyboardMarkup(rows);
        }

        private static InlineKeyboardMarkup CreateOnOffButtons()
        {
            var rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[2];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("On", "hardware-automation-on");
            rows[0][1] = InlineKeyboardButton.WithCallbackData("Off", "hardware-automation-off");

            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Toggle", "hardware-automation-toggle");

            rows[2] = new InlineKeyboardButton[1];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("<<", "automation");

            return new InlineKeyboardMarkup(rows);
        }

        private static ReplyKeyboardMarkup CreateHardwareToggles()
        {
            var keyboard =
                new KeyboardButton[][]
                {
                    [
                        new KeyboardButton(HardwareEmoji.Bulb),
                        new KeyboardButton(HardwareEmoji.Thunder)
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.Pc),
                        new KeyboardButton(HardwareEmoji.Reboot)
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.On),
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.Off),
                    ],

                };
            return new ReplyKeyboardMarkup(keyboard);
        }
    }
}