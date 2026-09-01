using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CortanaLib.Client;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Runtime;

/// What a topic is currently waiting for the user to type
public sealed record PendingInput(string Kind, CallbackQuery Query, Message Message, string Argument = "")
{
	public List<int> Transcript { get; } = [];
}

public sealed record IncomingText(int TopicId, int MessageId, string Text, string Command);

/// Shared state and helpers for the bot
public static partial class TelegramSession
{
	public static readonly TelegramConfig Config = TelegramConfig.Load();
	public static readonly CortanaClient Cortana = CortanaClient.Default.As(CommandSurface.Telegram);
	public static readonly ConcurrentDictionary<int, PendingInput> Pending = new();

	private static ITelegramBotClient _bot = null!;

	[GeneratedRegex(@"^([0-9]+)([smhd])$", RegexOptions.Compiled)] private static partial Regex TimePattern { get; }

	public static ITelegramBotClient Bot => _bot;

	public static long HomeId => Config.HomeGroup;

	public static Topics Topics => Config.Topics;

	public static void Use(ITelegramBotClient bot) => _bot = bot;

	public static Task<Message> Post(string text, int topicId, ParseMode parseMode = ParseMode.None,
		ReplyMarkup? markup = null, bool silent = false) =>
		_bot.SendMessage(new ChatId(HomeId), text, messageThreadId: topicId, replyMarkup: markup, parseMode: parseMode, disableNotification: silent);

	public static Task Whisper(long userId, string text) => _bot.SendMessage(new ChatId(userId), text);

	public static bool Begin(int topicId, PendingInput input, CallbackQuery query)
	{
		if (Pending.TryAdd(topicId, input)) return true;

		_ = _bot.AnswerCallbackQuery(query.Id, "Finish the interaction you already started first", true);
		return false;
	}

	public static void End(int topicId) => Pending.TryRemove(topicId, out _);

	public static void Ack(CallbackQuery query, string text = "", bool alert = false)
	{
		_ = _bot.AnswerCallbackQuery(query.Id, text, alert).ContinueWith(
			task => Log.Write("Telegram", $"Could not answer a callback: {task.Exception?.GetBaseException().Message}"),
			TaskContinuationOptions.OnlyOnFaulted);
	}

	/// Feedback for an action whose callback query was already answered
	public static void Toast(int topicId, string text, TimeSpan? life = null)
	{
		if (string.IsNullOrWhiteSpace(text)) return;

		_ = Task.Run(async () =>
		{
			try
			{
				Message sent = await Post(text, topicId, silent: true);
				await Task.Delay(life ?? TimeSpan.FromSeconds(8));
				await _bot.DeleteMessage(HomeId, sent.MessageId);
			}
			catch (Exception ex)
			{
				Log.Write("Telegram", $"Could not show a toast: {ex.Message}");
			}
		});
	}

	public static async Task Delete(int messageId)
	{
		try
		{
			await _bot.DeleteMessage(HomeId, messageId);
		}
		catch (Exception ex)
		{
			Log.Write("Telegram", $"Could not delete a message: {ex.Message}");
		}
	}

	public static TimeSpan? ParseDuration(string text)
	{
		var total = TimeSpan.Zero;

		foreach (string part in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
		{
			Match match = TimePattern.Match(part.Trim());
			if (!match.Success) return null;

			int value = int.Parse(match.Groups[1].Value);
			total += match.Groups[2].Value switch
			{
				"s" => TimeSpan.FromSeconds(value),
				"m" => TimeSpan.FromMinutes(value),
				"h" => TimeSpan.FromHours(value),
				_ => TimeSpan.FromDays(value)
			};
		}

		return total == TimeSpan.Zero ? null : total;
	}

	public static InlineKeyboardMarkup Cancel(string tag) => new InlineKeyboardMarkup().AddButton("<<", $"{tag}-cancel");

	public static async Task<string> Text(Task<Result<string>> call) => (await call).Match(value => value, error => error);
}
