using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types.Enums;
using Processor;

namespace TelegramBot.Modules
{
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
        
        public static async void CreateAutomationMenu(ITelegramBotClient cortana, CallbackQuery callbackQuery)
        {
            Message message = callbackQuery.Message!;
            HardwareAction.Remove(message.Id);
            
            if (TelegramUtils.CheckPermission(callbackQuery.From.Id))
                await cortana.EditMessageText(message.Chat.Id, message.Id, "Hardware Keyboard", replyMarkup: CreateAutomationButtons());
            else
                await cortana.AnswerCallbackQuery(callbackQuery.Id, "Sorry, you can't use this command");
        }
        
        public static async void CreateRaspberryMenu(ITelegramBotClient cortana, CallbackQuery callbackQuery)
        {
            Message message = callbackQuery.Message!;
            
            if (TelegramUtils.CheckPermission(callbackQuery.From.Id))
                await cortana.EditMessageText(message.Chat.Id, message.Id, "Raspberry Handler", replyMarkup: CreateRaspberryButtons());
            else
                await cortana.AnswerCallbackQuery(callbackQuery.Id, "Sorry, you can't access raspberry's controls");
        }
        
        public static async void CreateHardwareUtilityMenu(ITelegramBotClient cortana, Message message)
        {
            await cortana.EditMessageText(message.Chat.Id, message.Id, "Hardware Utility", replyMarkup: CreateUtilityButtons());
        }
        
        public static async void HandleKeyboardCallback(ITelegramBotClient cortana, MessageStats messageStats)
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
        
        public static async void HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
        {
            int messageId = callbackQuery.Message!.MessageId;

            if (command.StartsWith("raspberry-"))
            {
                switch (command["raspberry-".Length..])
                {
                    case "ip":
                        string ip = await Hardware.GetPublicIp();
                        await cortana.AnswerCallbackQuery(callbackQuery.Id, $"IP: {ip}");
                        break;
                    case "gateway":
                        string gateway = Hardware.GetDefaultGateway();
                        await cortana.AnswerCallbackQuery(callbackQuery.Id,  $"Gateway: {gateway}");
                        break;
                    case "location":
                        string location = Hardware.GetLocation();
                        await cortana.AnswerCallbackQuery(callbackQuery.Id,  $"Location: {location}");
                        break;
                    case "temperature":
                        string temp = Hardware.GetCpuTemperature();
                        await cortana.AnswerCallbackQuery(callbackQuery.Id,  $"Temperature: {temp}");
                        break;
                    case "reboot":
                        string rebootResult = Hardware.PowerRaspberry(EPowerOption.Reboot);
                        await cortana.AnswerCallbackQuery(callbackQuery.Id,  rebootResult, true);
                        break;
                    case "shutdown":
                        string shutdownResult = Hardware.PowerRaspberry(EPowerOption.Shutdown);
                        await cortana.AnswerCallbackQuery(callbackQuery.Id,  shutdownResult, true);
                        break;
                }
            }
            else if (command.StartsWith("automation-"))
            {
                command = command["automation-".Length..];

                if (!HardwareAction.TryAdd(messageId, command))
                {
                    string result = Hardware.SwitchFromString(HardwareAction[messageId], command);
                    await cortana.AnswerCallbackQuery(callbackQuery.Id, result);
                    return;
                }
                await cortana.EditMessageReplyMarkup(callbackQuery.Message.Chat.Id, messageId, replyMarkup: CreateOnOffButtons());
            }
            else if (command.StartsWith("utility-"))
            {
                long chatId = callbackQuery.Message.Chat.Id;
                
                switch (command["utility-".Length..])
                {
                    case "notify":
                        await cortana.SendChatAction(chatId, ChatAction.Typing);
                        if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Notification, callbackQuery, callbackQuery.Message),callbackQuery))
                            await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton());
                        break;
                    case "ping":
                        await cortana.SendChatAction(chatId, ChatAction.FindLocation);
                        if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Ping, callbackQuery, callbackQuery.Message),callbackQuery))
                            await cortana.EditMessageText(chatId, messageId, "Write the IP of the host you want to ping", replyMarkup: CreateCancelButton());
                        break;
                    case "cancel":
                        TelegramUtils.ChatArgs.Remove(chatId);
                        CreateHardwareUtilityMenu(cortana, callbackQuery.Message);
                        return;
                }
            }
        }
        
        public static async void HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
        {
            switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
            {
                case ETelegramChatArg.Notification:
                    string result = Hardware.CommandPc(EComputerCommand.Notify, messageStats.FullMessage);
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    TelegramUtils.AnswerOrMessage(cortana, result, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
                    CreateHardwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
                    TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
                    break;
                case ETelegramChatArg.Ping:
                    string output = Hardware.Ping(messageStats.FullMessage) ? "Host reached successfully!" : "Host could not be reached!";
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    TelegramUtils.AnswerOrMessage(cortana, output, messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
                    CreateHardwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
                    TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
                    break;
            }
        }
        
        private static InlineKeyboardMarkup CreateAutomationButtons()
        {
            InlineKeyboardMarkup inlineKeyboard = new InlineKeyboardMarkup()
                .AddButton("Room", "hardware-automation-room")
                .AddNewRow();
            
            foreach (string element in Enum.GetNames(typeof(EGpio)))
            {
                inlineKeyboard.AddButton(element, $"hardware-automation-{element.ToLower()}");
                inlineKeyboard.AddNewRow();
            }
            inlineKeyboard.AddButton("<<", "home");
            return inlineKeyboard;
        }
        
        private static InlineKeyboardMarkup CreateRaspberryButtons()
        {
            return new InlineKeyboardMarkup()
                .AddButton("Shutdown", "hardware-raspberry-shutdown")
                .AddButton("Reboot", "hardware-raspberry-reboot")
                .AddNewRow()
                .AddButton("Temperature", "hardware-raspberry-temperature")
                .AddButton("IP", "hardware-raspberry-ip")
                .AddNewRow()
                .AddButton("Location", "hardware-raspberry-location")
                .AddButton("Gateway", "hardware-raspberry-gateway")
                .AddNewRow()
                .AddButton("<<", "home");
        }
        
        private static InlineKeyboardMarkup CreateUtilityButtons()
        {
            return new InlineKeyboardMarkup()
                .AddButton("Ping", "hardware-utility-ping")
                .AddNewRow()
                .AddButton("Desktop Notification", "hardware-utility-notify")
                .AddNewRow()
                .AddButton("<<", "home");
        }

        private static InlineKeyboardMarkup CreateOnOffButtons()
        {
            return new InlineKeyboardMarkup()
                .AddButton("On", "hardware-automation-on")
                .AddButton("Off", "hardware-automation-off")
                .AddNewRow()
                .AddButton("Toggle", "hardware-automation-toggle")
                .AddNewRow()
                .AddButton("<<", "automation");
        }
        
        private static InlineKeyboardMarkup CreateCancelButton()
        {
            return new InlineKeyboardMarkup()
                .AddButton("<<", "hardware-utility-cancel");
        }

        private static ReplyKeyboardMarkup CreateHardwareToggles()
        {
            return new ReplyKeyboardMarkup(true)
                .AddButtons(HardwareEmoji.Bulb, HardwareEmoji.Thunder)
                .AddNewRow()
                .AddButtons(HardwareEmoji.Pc, HardwareEmoji.Reboot)
                .AddNewRow()
                .AddButton(HardwareEmoji.On)
                .AddNewRow()
                .AddButton(HardwareEmoji.Off);
        }
    }
    
    internal static class HardwareEmoji
    {
        public const string Bulb = "💡";
        public const string Pc = "🖥";
        public const string Thunder = "⚡";
        public const string Reboot = "🔄";
        public const string On = "\ud83c\udf15\ud83c\udf15\ud83c\udf15";
        public const string Off = "\ud83c\udf11\ud83c\udf11\ud83c\udf11";
    }
}