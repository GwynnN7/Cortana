using Telegram.Bot;
using Telegram.Bot.Types;
using Utility;

namespace TelegramBot.Modules;

public static class UtilityModule
{
    enum EAnswerCommands { QRCODE, CHAT }
    struct AnswerCommand
    {
        public EAnswerCommands Command;
        public string? CommandValue;
        public AnswerCommand(EAnswerCommands cmd, string? cmdVal = null)
        {
            Command = cmd;
            CommandValue = cmdVal;
        }
    }
    private static Dictionary<long, AnswerCommand> AnswerCommands = new();
    
    public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
    {
        switch (messageStats.Command)
        {
            case "location":
                await cortana.SendTextMessageAsync(messageStats.ChatID, HardwareDriver.GetLocation());
                break;
            case "qrcode":
                AnswerCommands.Remove(messageStats.ChatID);
                AnswerCommands.Add(messageStats.ChatID, new AnswerCommand(EAnswerCommands.QRCODE));
                await cortana.SendTextMessageAsync(messageStats.ChatID, "Scrivi il contenuto");
                break;
            case "send":
                if (TelegramData.CheckPermission(messageStats.UserID))
                {
                    if (messageStats.TextList.Count > 1)
                    {
                        TelegramData.SendToUser(TelegramData.NameToID(messageStats.TextList[0]), string.Join(" ", messageStats.TextList.Skip(1)));
                        await cortana.SendTextMessageAsync(messageStats.ChatID, $"Testo inviato a {messageStats.TextList[0]}");
                    }
                    else await cortana.SendTextMessageAsync(messageStats.ChatID, "Errore nel numero dei parametri");
                }
                else await cortana.SendTextMessageAsync(messageStats.ChatID, "Non hai l'autorizzazione per eseguire questo comando");
                break;
            case "join":
                if (TelegramData.CheckPermission(messageStats.UserID))
                {
                    if (messageStats.TextList.Count == 1)
                    {
                        AnswerCommands.Remove(messageStats.ChatID);
                        AnswerCommands.Add(messageStats.ChatID, new AnswerCommand(EAnswerCommands.CHAT, messageStats.TextList[0]));
                        await cortana.SendTextMessageAsync(messageStats.ChatID, $"Chat con {messageStats.TextList[0]} avviata");
                    }
                    else await cortana.SendTextMessageAsync(messageStats.ChatID, "Errore nel numero dei parametri");
                }
                else await cortana.SendTextMessageAsync(messageStats.ChatID, "Non hai l'autorizzazione per eseguire questo comando");
                break;
            case "leave":
                if (AnswerCommands.ContainsKey(messageStats.ChatID) && AnswerCommands[messageStats.ChatID].Command == EAnswerCommands.CHAT)
                {
                    await cortana.SendTextMessageAsync(messageStats.ChatID, $"Chat con {AnswerCommands[messageStats.ChatID].CommandValue} terminata");
                    AnswerCommands.Remove(messageStats.ChatID);
                }
                break;
        }
    }
    
    public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
    {
        switch (AnswerCommands[messageStats.ChatID].Command)
        {
            case EAnswerCommands.QRCODE:
                var imageStream = Utility.Functions.CreateQRCode(content: messageStats.FullMessage, useNormalColors: false, useBorders: true);
                imageStream.Position = 0;
                await cortana.SendPhotoAsync(messageStats.ChatID, new InputFileStream(imageStream, "QRCODE.png"));
                AnswerCommands.Remove(messageStats.ChatID);
                break;
            case EAnswerCommands.CHAT:
                TelegramData.SendToUser(TelegramData.NameToID(AnswerCommands[messageStats.ChatID].CommandValue!), messageStats.FullMessage);
                break;
        }
    }

    public static bool IsWaiting(long chatId)
    {
        return AnswerCommands.ContainsKey(chatId);
    }
}