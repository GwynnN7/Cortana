using System.Collections.Concurrent;
using CortanaLib.Runtime;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// Keeps the visible menu of each topic in step with the Kernel, updating at each Interval
public static class LiveMenu
{
	private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
	private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(10);

	private static readonly ConcurrentDictionary<int, Entry> Entries = new();
	private static readonly SemaphoreSlim Pass = new(1, 1);

	private static DateTimeOffset _lastPass = DateTimeOffset.MinValue;
	private static int _pending;

	private sealed record Entry(Menu Menu, int MessageId, DateTimeOffset ExpiresAt)
	{
		public string LastText { get; set; } = "";
		public string LastKeyboard { get; set; } = "";
	}

	public static void Track(Menu menu, int messageId, string text, InlineKeyboardMarkup keyboard) =>
		Entries[menu.Topic] = new Entry(menu, messageId, DateTimeOffset.Now + Lifetime)
		{
			LastText = text,
			LastKeyboard = Describe(keyboard)
		};

	public static void Nudge() => Interlocked.Exchange(ref _pending, 1);

	public static async Task Run(CancellationToken token)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

		while (await timer.WaitForNextTickAsync(token))
		{
			bool due = Interlocked.Exchange(ref _pending, 0) == 1 || DateTimeOffset.Now - _lastPass > TimeSpan.FromMinutes(2);
			if (due) await Refresh();
		}
	}

	public static async Task Refresh()
	{
		if (Entries.IsEmpty) return;
		if (DateTimeOffset.Now - _lastPass < MinimumInterval) return;
		if (!await Pass.WaitAsync(0)) return;

		try
		{
			_lastPass = DateTimeOffset.Now;

			foreach ((int topicId, Entry entry) in Entries.ToArray())
			{
				if (DateTimeOffset.Now >= entry.ExpiresAt)
				{
					Entries.TryRemove(topicId, out _);
					continue;
				}

				if (TelegramSession.Pending.ContainsKey(topicId)) continue;

				await Update(topicId, entry);
			}
		}
		finally
		{
			Pass.Release();
		}
	}

	private static async Task Update(int topicId, Entry entry)
	{
		try
		{
			string text = await entry.Menu.RenderForRefresh();
			InlineKeyboardMarkup keyboard = entry.Menu.KeyboardForRefresh();
			string shape = Describe(keyboard);

			if (text == entry.LastText && shape == entry.LastKeyboard) return;

			await TelegramSession.Bot.EditMessageText(TelegramSession.HomeId, entry.MessageId, text,
				replyMarkup: keyboard, parseMode: ParseMode.Html);

			entry.LastText = text;
			entry.LastKeyboard = shape;
		}
		catch (Exception ex) when (ex.Message.Contains("message is not modified", StringComparison.OrdinalIgnoreCase))
		{
			entry.LastText = "";
		}
		catch (Exception ex)
		{
			Log.Write("Telegram", $"Live menu for topic {topicId} dropped: {ex.Message}");
			Entries.TryRemove(topicId, out _);
		}
	}

	private static string Describe(InlineKeyboardMarkup keyboard) =>
		string.Join("|", keyboard.InlineKeyboard.SelectMany(row => row.Select(button => $"{button.Text}:{button.CallbackData}")));
}
