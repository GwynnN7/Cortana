using System.Collections.Concurrent;
using CortanaLib;
using CortanaLib.Structures;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Timer = CortanaLib.Structures.Timer;

namespace CortanaTelegram.Utility;

internal static class LiveMenu
{
	private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);
	private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

	private static readonly ConcurrentDictionary<int, Entry> Entries = new();
	private static readonly Lock TimerLock = new();
	private static Timer? _ticker;

	private sealed record Entry(int MessageId, Func<Task<string>> Render, Func<InlineKeyboardMarkup> Keyboard, DateTime ExpiresAt);

		public static void Track(int topicId, int messageId, Func<Task<string>> render, Func<InlineKeyboardMarkup> keyboard)
	{
		Entries[topicId] = new Entry(messageId, render, keyboard, DateTime.Now.Add(DefaultLifetime));
		EnsureTicker();
	}

	public static void Release(int topicId) => Entries.TryRemove(topicId, out _);

	private static void EnsureTicker()
	{
		lock (TimerLock)
		{
			if (_ticker != null) return;
			_ticker = new Timer("telegram-live-menu", null, Tick, ETimerType.Telegram, ETimerLoop.Interval)
				.Set(((int)TickInterval.TotalSeconds, 0, 0));
		}
	}

	private static void StopTicker()
	{
		lock (TimerLock)
		{
			_ticker?.Destroy();
			_ticker = null;
		}
	}

	private static async Task Tick(object? sender)
	{
		if (Entries.IsEmpty)
		{
			StopTicker();
			return;
		}

		foreach ((int topicId, Entry entry) in Entries.ToArray())
		{
			if (DateTime.Now >= entry.ExpiresAt)
			{
				Entries.TryRemove(topicId, out _);
				continue;
			}

			if (Utils.ChatArgs.ContainsKey(topicId)) continue;

			try
			{
				string text = await entry.Render();
				await Utils.Bot.EditMessageText(Utils.HomeId, entry.MessageId, text, replyMarkup: entry.Keyboard(), parseMode: ParseMode.Html);
			}
			catch (Exception ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
			{
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Telegram] Live menu {topicId} dropped: {ex.Message}");
				Entries.TryRemove(topicId, out _);
			}
		}
	}
}
