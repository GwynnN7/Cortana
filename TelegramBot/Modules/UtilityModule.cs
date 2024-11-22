using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;
using Telegram.Bot.Types.Enums;

namespace TelegramBot.Modules;

internal enum EAnswerCommands { Qrcode, Chat, Notification }

internal struct AnswerCommand(EAnswerCommands cmd, CallbackQuery callbackQuery, Message interactionMessage, string? cmdVal = null)
{
    public readonly CallbackQuery CallbackQuery = callbackQuery;
    public readonly Message InteractionMessage = interactionMessage;
    public readonly EAnswerCommands Command = cmd;
    public readonly string? CommandValue = cmdVal;
}

public static class UtilityModule
{
    private static readonly Dictionary<long, AnswerCommand> AnswerCommands = new();
    
    public static async void CreateUtilityMenu(ITelegramBotClient cortana, Message message)
    {
        await cortana.EditMessageText(message.Chat.Id, message.Id, "Utility Functions", replyMarkup: CreateUtilityButtons());
    }
    
    public static async void HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
        {
            int messageId = callbackQuery.Message!.MessageId;
            long chatId = callbackQuery.Message.Chat.Id;

            if (AnswerCommands.ContainsKey(chatId))
            {
                switch (command)
                {
                    case "cancel":
                        AnswerCommands.Remove(chatId);
                        CreateUtilityMenu(cortana, callbackQuery.Message);
                        return;
                    case "leave":
                        if (!AnswerCommands.TryGetValue(chatId, out AnswerCommand cmd) || cmd.Command != EAnswerCommands.Chat) return;
                        await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Chat with {cmd.CommandValue} ended");
                        AnswerCommands.Remove(chatId);
                        CreateUtilityMenu(cortana, callbackQuery.Message);
                        return;
                    default:
                        await cortana.AnswerCallbackQuery(callbackQuery.Id, "You already have an interaction going on", true);
                        return;
                }
            }
            
            switch (command)
            {
                case "qrcode":
                    AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Qrcode, callbackQuery, callbackQuery.Message));
                    await cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton());
                    break;
                case "notify":
                    AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Notification, callbackQuery, callbackQuery.Message));
                    await cortana.EditMessageText(chatId, messageId, "Write the content of the message", replyMarkup: CreateCancelButton());
                    break;
                case "join":
                    if (callbackQuery.Message.Chat.Type == ChatType.Private)
                    {
                        AnswerCommands.Add(chatId, new AnswerCommand(EAnswerCommands.Chat, callbackQuery, callbackQuery.Message));
                        await cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton());
                    }
                    else await cortana.AnswerCallbackQuery(callbackQuery.Id, "You can only use this command in private chat", true);
                    break;
            }
        }

    public static async void HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
    {
        switch (AnswerCommands[messageStats.ChatId].Command)
        {
            case EAnswerCommands.Qrcode:
                Stream imageStream = Software.CreateQrCode(content: messageStats.FullMessage, useNormalColors: false, useBorders: true);
                imageStream.Position = 0;
                await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                await cortana.SendPhoto(messageStats.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
                CreateUtilityMenu(cortana, AnswerCommands[messageStats.ChatId].InteractionMessage);
                AnswerCommands.Remove(messageStats.ChatId);
                break;
            case EAnswerCommands.Notification:
                string result = Hardware.CommandPc(EComputerCommand.Notify, messageStats.FullMessage);
                await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                AnswerOrMessage(cortana, result, messageStats.ChatId, AnswerCommands[messageStats.ChatId].CallbackQuery);
                CreateUtilityMenu(cortana, AnswerCommands[messageStats.ChatId].InteractionMessage);
                AnswerCommands.Remove(messageStats.ChatId);
                break;
            case EAnswerCommands.Chat:
                if (AnswerCommands[messageStats.ChatId].CommandValue != null)
                {
                    TelegramUtils.SendToUser(long.Parse(AnswerCommands[messageStats.ChatId].CommandValue!), messageStats.FullMessage);
                    break;
                }

                try
                {
                    string user = messageStats.FullMessage.Trim();
                    AnswerCommands[messageStats.ChatId] = new AnswerCommand(EAnswerCommands.Chat, AnswerCommands[messageStats.ChatId].CallbackQuery, AnswerCommands[messageStats.ChatId].InteractionMessage, TelegramUtils.NameToId(user).ToString());
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    await cortana.EditMessageText(messageStats.ChatId, AnswerCommands[messageStats.ChatId].InteractionMessage.MessageId, $"Currently chatting with {user}", replyMarkup: CreateLeaveButton());
                }
                catch
                {
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    AnswerOrMessage(cortana, "Sorry, I can't find that username. Please try again", messageStats.ChatId, AnswerCommands[messageStats.ChatId].CallbackQuery);
                }

                break;
        }
    }

    private static InlineKeyboardMarkup CreateUtilityButtons()
    {
        return new InlineKeyboardMarkup()
            .AddButton("QRCode", "utility-qrcode")
            .AddNewRow()
            .AddButton("Desktop Notification", "utility-notify")
            .AddNewRow()
            .AddButton("Start Chat", "utility-join")
            .AddNewRow()
            .AddButton(InlineKeyboardButton.WithUrl("Cortana", "https://github.com/GwynbleiddN7/Cortana"))
            .AddNewRow()
            .AddButton("<<", "home");
    }
    
    private static InlineKeyboardMarkup CreateLeaveButton()
    {
        return new InlineKeyboardMarkup()
            .AddButton("Stop Chat", "utility-leave");
    }
    
    private static InlineKeyboardMarkup CreateCancelButton()
    {
        return new InlineKeyboardMarkup()
            .AddButton("<<", "utility-cancel");
    }
    
    private static async void AnswerOrMessage(ITelegramBotClient cortana, string text, long chatId, CallbackQuery? callbackQuery)
    {
        try { await cortana.AnswerCallbackQuery(callbackQuery!.Id, text, true); }
        catch { await cortana.SendMessage(chatId, text); }
    }

    public static bool IsWaiting(long chatId) => AnswerCommands.ContainsKey(chatId);
}