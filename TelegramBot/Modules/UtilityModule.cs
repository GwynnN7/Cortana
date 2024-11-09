using Telegram.Bot;
using Telegram.Bot.Types;
using Processor;

namespace TelegramBot.Modules;

internal enum EAnswerCommands { Qrcode, Chat }

internal struct AnswerCommand(EAnswerCommands cmd, string? cmdVal = null)
{
    public readonly EAnswerCommands Command = cmd;
    public readonly string? CommandValue = cmdVal;
}

public static class UtilityModule
{
    private static readonly Dictionary<long, AnswerCommand> AnswerCommands = new();
    
    public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
    {
        switch (messageStats.Command)
        {
            case "location":
                await cortana.SendMessage(messageStats.ChatId, Hardware.GetLocation());
                break;
            case "qrcode":
                AnswerCommands.Remove(messageStats.ChatId);
                AnswerCommands.Add(messageStats.ChatId, new AnswerCommand(EAnswerCommands.Qrcode));
                await cortana.SendMessage(messageStats.ChatId, "Scrivi il contenuto");
                break;
            case "send":
                if (TelegramUtils.CheckPermission(messageStats.UserId))
                {
                    if (messageStats.TextList.Count > 1)
                    {
                        TelegramUtils.SendToUser(TelegramUtils.NameToId(messageStats.TextList[0]), string.Join(" ", messageStats.TextList.Skip(1)));
                        await cortana.SendMessage(messageStats.ChatId, $"Testo inviato a {messageStats.TextList[0]}");
                    }
                    else await cortana.SendMessage(messageStats.ChatId, "Errore nel numero dei parametri");
                }
                else await cortana.SendMessage(messageStats.ChatId, "Non hai l'autorizzazione per eseguire questo comando");
                break;
            case "join":
                if (TelegramUtils.CheckPermission(messageStats.UserId))
                {
                    if (messageStats.TextList.Count == 1)
                    {
                        AnswerCommands.Remove(messageStats.ChatId);
                        AnswerCommands.Add(messageStats.ChatId, new AnswerCommand(EAnswerCommands.Chat, messageStats.TextList[0]));
                        await cortana.SendMessage(messageStats.ChatId, $"Chat con {messageStats.TextList[0]} avviata");
                    }
                    else await cortana.SendMessage(messageStats.ChatId, "Errore nel numero dei parametri");
                }
                else await cortana.SendMessage(messageStats.ChatId, "Non hai l'autorizzazione per eseguire questo comando");
                break;
            case "leave":
                if (AnswerCommands.TryGetValue(messageStats.ChatId, out AnswerCommand command) && command.Command == EAnswerCommands.Chat)
                {
                    await cortana.SendMessage(messageStats.ChatId, $"Chat con {command.CommandValue} terminata");
                    AnswerCommands.Remove(messageStats.ChatId);
                }
                break;
        }
    }
    
    public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
    {
        switch (AnswerCommands[messageStats.ChatId].Command)
        {
            case EAnswerCommands.Qrcode:
                Stream imageStream = Software.CreateQrCode(content: messageStats.FullMessage, useNormalColors: false, useBorders: true);
                imageStream.Position = 0;
                await cortana.SendPhoto(messageStats.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
                AnswerCommands.Remove(messageStats.ChatId);
                break;
            case EAnswerCommands.Chat:
                TelegramUtils.SendToUser(TelegramUtils.NameToId(AnswerCommands[messageStats.ChatId].CommandValue!), messageStats.FullMessage);
                break;
        }
    }

    public static bool IsWaiting(long chatId)
    {
        return AnswerCommands.ContainsKey(chatId);
    }
}