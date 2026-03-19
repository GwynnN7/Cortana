using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Utility;

internal static class Utils
{
	private static ConnectionMultiplexer CommunicationClient { get; }
	private static TelegramBotClient _cortana = null!;
	private static readonly Regex TimeRegex = new("^([0-9]+)([smhd])$", RegexOptions.Compiled);

	public static readonly ConcurrentDictionary<long, ChatArgs> ChatArgs;
	public static readonly DataStruct Data;
	public static readonly long AuthorId;
	public static long HomeId => Data.HomeGroup;
	public static Topics Topics => Data.Topics;

	static Utils()
	{
		Data = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaTelegram)}/Data.json").Load<DataStruct>();
		ChatArgs = new ConcurrentDictionary<long, ChatArgs>();

		AuthorId = NameToId("@gwynn7");

		CommunicationClient = ConnectionMultiplexer.Connect("localhost");

		ISubscriber ipc = CommunicationClient.GetSubscriber();
		ipc.Subscribe(RedisChannel.Literal(EMessageCategory.Telegram.ToString())).OnMessage(async channelMessage =>
		{
			if (channelMessage.Message.HasValue) await SendToTopic(channelMessage.Message.ToString(), Topics.Log);
		});
	}

	public static void Init(TelegramBotClient newClient)
	{
		_cortana = newClient;
	}

	public static async Task<Message> SendToTopic(string message, int topicId, ReplyMarkup? replyMarkup = null)
	{
		var chat = new ChatId(HomeId);
		return await _cortana.SendMessage(chat, message, messageThreadId: topicId, replyMarkup: replyMarkup);
	}

	public static async Task SendToUser(long userId, string message)
	{
		var chat = new ChatId(userId);
		await _cortana.SendMessage(chat, message);
	}

	public static string IdToName(long id)
	{
		return Data.Usernames.GetValueOrDefault(id, "Unknown user");
	}

	public static long NameToId(string name)
	{
		foreach ((long nameId, _) in Data.Usernames.Where(item => item.Value == name))
			return nameId;
		throw new CortanaException("User not found");
	}

	public static bool AddChatArg(long chatId, ChatArgs arg, CallbackQuery query)
	{
		if (ChatArgs.TryAdd(chatId, arg)) return true;
		_cortana.AnswerCallbackQuery(query.Id, "You already have an interaction going on! Finish it before continuing", true);
		return false;
	}

	public static (int, int, int, int) ParseTime(string text)
	{
		(int s, int m, int h, int d) times = (0, 0, 0, 0);
		foreach (string time in text.Split())
		{
			Match match = TimeRegex.Match(time);
			if (match.Success)
			{
				int value = int.Parse(match.Groups[1].Value);
				switch (match.Groups[2].Value)
				{
					case "s":
						times.s = value;
						break;
					case "m":
						times.m = value;
						break;
					case "h":
						times.h = value;
						break;
					case "d":
						times.h = value * 24;
						break;
				}
			}
			else throw new CortanaException("Invalid time");
		}
		return times;
	}

	public static async Task AnswerMessage(ITelegramBotClient cortana, string text, int topicId, CallbackQuery? callbackQuery, bool showAlert = true)
	{
		try
		{
			await cortana.AnswerCallbackQuery(callbackQuery!.Id, text, showAlert);
		}
		catch
		{
			await cortana.SendMessage(HomeId, text, messageThreadId: topicId);
		}
	}

	public static void Shutdown()
	{
		CommunicationClient.Close();
		CommunicationClient.Dispose();
	}
}