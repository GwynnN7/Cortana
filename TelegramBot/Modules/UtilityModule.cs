using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;
using Telegram.Bot.Types.Enums;

namespace TelegramBot.Modules;

internal enum EAnswerCommands { Qrcode, Chat, Notification }

internal struct AnswerCommand(EAnswerCommands cmd, Update update, Message interactionMessage, string? cmdVal = null)
{
    public readonly Update Update = update;
    public readonly Message InteractionMessage = interactionMessage;
    public readonly EAnswerCommands Command = cmd;
    public readonly string? CommandValue = cmdVal;
}

public static class UtilityModule
{
    private static readonly Dictionary<long, AnswerCommand> AnswerCommands = new();
    
    public static async void ButtonCallback(ITelegramBotClient cortana, Update update, string command)
        {
            if(update.CallbackQuery == null) return;
            
            int messageId = update.CallbackQuery.Message!.MessageId;
            long chatId = update.CallbackQuery.Message.Chat.Id;

            if (AnswerCommands.ContainsKey(chatId))
            {
                switch (command)
                {
                    case "cancel":
                        AnswerCommands.Remove(chatId);
                        CreateUtilityMenu(cortana, update.CallbackQuery.Message);
                        return;
                    case "leave":
                        if (!AnswerCommands.TryGetValue(chatId, out AnswerCommand cmd) || cmd.Command != EAnswerCommands.Chat) return;
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, $"Chat with {cmd.CommandValue} ended");
                        AnswerCommands.Remove(chatId);
                        CreateUtilityMenu(cortana, update.CallbackQuery.Message);
                        return;
                    default:
                        await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, "You already have an interaction going on", true);
                        return;
                }
            }
            
            switch (command)
            {
                case "qrcode":
                    AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Qrcode, update, update.CallbackQuery.Message));
                    await cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton());
                    break;
                case "notify":
                    AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Notification, update, update.CallbackQuery.Message));
                    await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton());
                    break;
                case "join":
                    if (update.CallbackQuery.Message.Chat.Type == ChatType.Private)
                    {
                        AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Chat, update, update.CallbackQuery.Message));
                        await cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton());
                    }
                    else await cortana.AnswerCallbackQuery(update.CallbackQuery.Id, "You can only use this command in private chat", true);
                    break;
            }
        }
    
    public static async void CreateUtilityMenu(ITelegramBotClient cortana, Message message)
    {
        await cortana.EditMessageText(message.Chat.Id, message.Id, "Utility Functions", replyMarkup: CreateUtilityButtons());
    }
    
    private static InlineKeyboardMarkup CreateUtilityButtons()
    {
        var rows = new InlineKeyboardButton[4][];
        
        rows[0] = new InlineKeyboardButton[1];
        rows[0][0] = InlineKeyboardButton.WithCallbackData("QRCode", "utility-qrcode");
        
        rows[1] = new InlineKeyboardButton[1];
        rows[1][0] = InlineKeyboardButton.WithCallbackData("Desktop Notification", "utility-notify");
        
        rows[2] = new InlineKeyboardButton[1];
        rows[2][0] = InlineKeyboardButton.WithCallbackData("Start Chat", "utility-join");
        
        rows[3] = new InlineKeyboardButton[1];
        rows[3][0] = InlineKeyboardButton.WithCallbackData("<<", "home");
        
        return new InlineKeyboardMarkup(rows);
    }
    
    private static InlineKeyboardMarkup CreateLeaveButton()
    {
        var rows = new InlineKeyboardButton[1][];
        
        rows[0] = new InlineKeyboardButton[1];
        rows[0][0] = InlineKeyboardButton.WithCallbackData("Stop Chat", "utility-leave");
        
        return new InlineKeyboardMarkup(rows);
    }
    
    private static InlineKeyboardMarkup CreateCancelButton()
    {
        var rows = new InlineKeyboardButton[1][];
        
        rows[0] = new InlineKeyboardButton[1];
        rows[0][0] = InlineKeyboardButton.WithCallbackData("<<", "utility-cancel");
        
        return new InlineKeyboardMarkup(rows);
    }
    
    public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
    {
        switch (AnswerCommands[messageStats.ChatId].Command)
        {
            case EAnswerCommands.Qrcode:
                Stream imageStream = Software.CreateQrCode(content: messageStats.FullMessage, useNormalColors: false, useBorders: true);
                imageStream.Position = 0;
                AnswerCommands.Remove(messageStats.ChatId);
                await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                await cortana.SendPhoto(messageStats.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
                CreateUtilityMenu(cortana, AnswerCommands[messageStats.ChatId].InteractionMessage);
                break;
            case EAnswerCommands.Notification:
                string result = Hardware.CommandPc(EComputerCommand.Notify, messageStats.FullMessage);
                AnswerCommands.Remove(messageStats.ChatId);
                if (result == "0") await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                else await cortana.SendMessage(messageStats.ChatId, result);
                CreateUtilityMenu(cortana, AnswerCommands[messageStats.ChatId].InteractionMessage);
                break;
            case EAnswerCommands.Chat:
                if (AnswerCommands[messageStats.ChatId].CommandValue != null)
                {
                    TelegramUtils.SendToUser(long.Parse(AnswerCommands[messageStats.ChatId].CommandValue!), messageStats.FullMessage);
                    break;
                }
                
                if (messageStats.TextList.Count == 1)
                {
                    try
                    {
                        AnswerCommands[messageStats.ChatId] = new AnswerCommand(EAnswerCommands.Chat, AnswerCommands[messageStats.ChatId].Update, AnswerCommands[messageStats.ChatId].InteractionMessage, TelegramUtils.NameToId(messageStats.TextList[0]).ToString());
                        await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                        await cortana.EditMessageText(messageStats.ChatId, AnswerCommands[messageStats.ChatId].InteractionMessage.MessageId, $"Currently chatting with {messageStats.TextList[0]}", replyMarkup: CreateLeaveButton());
                    }
                    catch
                    {
                        await cortana.SendMessage(messageStats.ChatId, "Sorry, I can't find that username. Please try again");
                    }
                }
                else await cortana.SendMessage(messageStats.ChatId, "Sorry, I can't understand the answer. Please try again with a single tag");
                break;
        }
    }

    public static bool IsWaiting(long chatId)
    {
        return AnswerCommands.ContainsKey(chatId);
    }
}