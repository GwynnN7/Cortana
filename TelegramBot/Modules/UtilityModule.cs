using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;
using Telegram.Bot.Types.Enums;
using File = System.IO.File;
using Video = YoutubeExplode.Videos.Video;

namespace TelegramBot.Modules;

public static class UtilityModule
{
    public static async void CreateSoftwareUtilityMenu(ITelegramBotClient cortana, Message message)
    {
        await cortana.EditMessageText(message.Chat.Id, message.Id, "Software Utility", replyMarkup: CreateUtilityButtons());
    }
    
    public static async void HandleCallbackQuery(ITelegramBotClient cortana, CallbackQuery callbackQuery, string command)
    {
        int messageId = callbackQuery.Message!.MessageId;
        long chatId = callbackQuery.Message.Chat.Id;
        
        switch (command)
        {
            case "qrcode":
                if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Qrcode, callbackQuery, callbackQuery.Message), callbackQuery))
                    await cortana.EditMessageText(chatId, messageId, "Write the content of the Qrcode", replyMarkup: CreateCancelButton());
                break;
            case "join":
                if (callbackQuery.Message.Chat.Type == ChatType.Private)
                {
                    if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.Chat, callbackQuery, callbackQuery.Message), callbackQuery))
                        await cortana.EditMessageText(chatId, messageId, "Tag of the user you want to start the chat with", replyMarkup: CreateLeaveButton());
                }
                else await cortana.AnswerCallbackQuery(callbackQuery.Id, "You can only use this command in private chat", true);
                break;
            case "music":
                if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.AudioDownloader, callbackQuery, callbackQuery.Message), callbackQuery))
                    await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the audio", replyMarkup: CreateCancelButton());
                break;
            case "video":
                if(TelegramUtils.TryAddChatArg(chatId, new TelegramChatArg(ETelegramChatArg.VideoDownloader, callbackQuery, callbackQuery.Message), callbackQuery))
                    await cortana.EditMessageText(chatId, messageId, "Write the YouTube url of the video", replyMarkup: CreateCancelButton());
                break;
            case "cancel":
                TelegramUtils.ChatArgs.Remove(chatId);
                CreateSoftwareUtilityMenu(cortana, callbackQuery.Message);
                return;
            case "leave":
                if (!TelegramUtils.ChatArgs.TryGetValue(chatId, out TelegramChatArg chatArg) || chatArg.Type != ETelegramChatArg.Chat) return;
                await cortana.AnswerCallbackQuery(callbackQuery.Id, $"Chat with {chatArg.ArgString} ended");
                TelegramUtils.ChatArgs.Remove(chatId);
                CreateSoftwareUtilityMenu(cortana, callbackQuery.Message);
                return;
        }
    }

    public static async void HandleTextMessage(ITelegramBotClient cortana, MessageStats messageStats)
    {
        switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
        {
            case ETelegramChatArg.Qrcode:
                await cortana.SendChatAction(messageStats.ChatId, ChatAction.UploadPhoto);
                Stream imageStream = Software.CreateQrCode(content: messageStats.FullMessage, useNormalColors: false, useBorders: true);
                imageStream.Position = 0;
                await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                await cortana.SendPhoto(messageStats.ChatId, new InputFileStream(imageStream, "QRCODE.png"));
                CreateSoftwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
                TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
                break;
            case ETelegramChatArg.Chat:
                await cortana.SendChatAction(messageStats.ChatId, ChatAction.Typing);
                if (TelegramUtils.ChatArgs[messageStats.ChatId].HasArg) {
                    TelegramUtils.SendToUser(TelegramUtils.ChatArgs[messageStats.ChatId].ArgLong, messageStats.FullMessage);
                    break;
                }

                try {
                    string user = messageStats.FullMessage.Trim();
                    TelegramUtils.ChatArgs[messageStats.ChatId] = new TelegramChatArg(ETelegramChatArg.Chat, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage, TelegramUtils.NameToId(user));
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    await cortana.EditMessageText(messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage.MessageId, $"Currently chatting with {user}", replyMarkup: CreateLeaveButton());
                }
                catch {
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    TelegramUtils.AnswerOrMessage(cortana, "Sorry, I can't find that username. Please try again", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
                }

                break;
            case ETelegramChatArg.AudioDownloader:
            case ETelegramChatArg.VideoDownloader:
                await cortana.SendChatAction(messageStats.ChatId, ChatAction.UploadVideo);
                try {
                    Video videoInfos = await Software.GetYoutubeVideoInfos(messageStats.FullMessage);
                    switch (TelegramUtils.ChatArgs[messageStats.ChatId].Type)
                    {
                        case ETelegramChatArg.VideoDownloader:
                        {
                            Software.DownloadVideo(messageStats.FullMessage);
                            if(File.Exists("video.mp4"))
                            {
                                Stream stream = File.OpenRead("video.mp4");
                                await cortana.SendVideo(messageStats.ChatId, InputFile.FromStream(stream, videoInfos.Title));
                                File.Delete("video.mp4");
                            }
                            else throw new CortanaException("Video not found");
                            
                            break;
                        }
                        case ETelegramChatArg.AudioDownloader:
                        {
                            Stream stream = await Software.GetAudioStream(messageStats.FullMessage);
                            await cortana.SendAudio(messageStats.ChatId, InputFile.FromStream(stream, videoInfos.Title));
                            break;
                        }
                    }
                }
                catch {
                    TelegramUtils.AnswerOrMessage(cortana, "Sorry, I can't find that video or it's too big", messageStats.ChatId, TelegramUtils.ChatArgs[messageStats.ChatId].CallbackQuery);
                }
                finally {
                    await cortana.DeleteMessage(messageStats.ChatId, messageStats.MessageId);
                    CreateSoftwareUtilityMenu(cortana, TelegramUtils.ChatArgs[messageStats.ChatId].InteractionMessage);
                    TelegramUtils.ChatArgs.Remove(messageStats.ChatId);
                }
                break;
        }
    }

    private static InlineKeyboardMarkup CreateUtilityButtons()
    {
        return new InlineKeyboardMarkup()
            .AddButton("QRCode", "utility-qrcode")
            .AddNewRow()
            .AddButton("Start Chat", "utility-join")
            .AddNewRow()
            .AddButton("Download Music", "utility-music")
            .AddNewRow()
            .AddButton("Download Video", "utility-video")
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
}