using System.Timers;

namespace Processor;

public abstract class Timer : System.Timers.Timer
{
	private static readonly Dictionary<ETimerType, List<Timer>> TotalTimers = new();

	public DateTime NextTargetTime;


	protected Timer(string name, string? text, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerType timerType, ETimerLoop loop, bool autoStart)
	{
		Name = name;
		Text = text;
		Interval = (hours * 3600 + minutes * 60 + seconds) * 1000;
		Callback = callback;
		TimerType = timerType;

		LoopType = loop;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

		Elapsed += TimerElapsed;
		AutoReset = false;
		if (autoStart) Start();

		SaveTimer();
	}

	protected Timer(string name, string? text, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerType timerType, ETimerLoop loop, bool autoStart)
	{
		Name = name;
		Text = text;
		Interval = targetTime.Subtract(DateTime.Now).Minutes <= 5 ? targetTime.AddDays(1).Subtract(DateTime.Now).TotalMilliseconds : targetTime.Subtract(DateTime.Now).TotalMilliseconds;
		Callback = callback;
		TimerType = timerType;

		LoopType = loop;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

		Elapsed += TimerElapsed;
		AutoReset = false;
		if (autoStart) Start();

		SaveTimer();
	}

	private string Name { get; }
	public string? Text { get; }
	private ETimerLoop LoopType { get; }
	private ETimerType TimerType { get; }
	private Action<object?, ElapsedEventArgs> Callback { get; }

	private void TimerElapsed(object? sender, ElapsedEventArgs args)
	{
		if (LoopType != ETimerLoop.No)
		{
			NextTargetTime = LoopType switch
			{
				ETimerLoop.Daily => DateTime.Now.AddDays(1),
				ETimerLoop.Weekly => DateTime.Now.AddDays(7),
				_ => DateTime.Now.AddMilliseconds(Interval)
			};

			double newInterval = NextTargetTime.Subtract(DateTime.Now).TotalMilliseconds;

			Interval = newInterval;
			Enabled = true;
			Start();
		}

		Callback.Invoke(sender, args);

		if (LoopType == ETimerLoop.No) RemoveTimer(this);
	}

	private void Destroy()
	{
		Stop();
		Dispose();
	}

	private void SaveTimer()
	{
		if (!TotalTimers.TryAdd(TimerType, [this])) TotalTimers[TimerType].Add(this);
	}

	public static void RemoveTimer(Timer timer)
	{
		foreach ((ETimerType timerType, List<Timer>? timerList) in TotalTimers)
		{
			if (!timerList.Contains(timer)) continue;
			TotalTimers[timerType].Remove(timer);
			timer.Destroy();
			break;
		}
	}

	public static void RemoveTimers(ETimerType timerType)
	{
		foreach ((ETimerType type, List<Timer>? timerHandlers) in TotalTimers)
		{
			if (type != timerType) continue;
			foreach (Timer listTimer in timerHandlers) RemoveTimer(listTimer);
		}
	}

	public static void RemoveTimerByName(string name)
	{
		foreach ((_, List<Timer>? timerHandlers) in TotalTimers)
		{
			foreach (Timer listTimer in timerHandlers.Where(listTimer => listTimer.Name == name))
			{
				RemoveTimer(listTimer);
				return;
			}
		}
	}

	public static List<DiscordTimer> GetDiscordTimers()
	{
		return TotalTimers[ETimerType.Discord].ConvertAll(timer => (DiscordTimer)timer);
	}

	public static List<TelegramTimer> GetTelegramTimers()
	{
		return TotalTimers[ETimerType.Telegram].ConvertAll(timer => (TelegramTimer)timer);
	}

	public static List<UtilityTimer> GetUtilityTimers()
	{
		return TotalTimers[ETimerType.Utility].ConvertAll(timer => (UtilityTimer)timer);
	}
}

public class DiscordTimer : Timer
{
	public DiscordTimer(object user, object? textChannel, string name, string? text, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No,
		bool autoStart = true) : base(name, text, targetTime, callback,
		ETimerType.Discord, loop, autoStart)
	{
		User = user;
		TextChannel = textChannel;
	}

	public DiscordTimer(object user, object? textChannel, string name, string? text, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No,
		bool autoStart = true) : base(name, text, hours,
		minutes, seconds, callback, ETimerType.Discord, loop, autoStart)
	{
		User = user;
		TextChannel = textChannel;
	}

	public object User { get; }
	public object? TextChannel { get; }
}

public class TelegramTimer : Timer
{
	public TelegramTimer(long userId, int chatId, string name, string? text, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) :
		base(name, text, targetTime, callback,
			ETimerType.Telegram, loop, autoStart)
	{
		ChatId = chatId;
		UserId = userId;
	}

	public TelegramTimer(long userId, int chatId, string name, string? text, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No,
		bool autoStart = true) : base(name, text, hours, minutes,
		seconds, callback, ETimerType.Telegram, loop, autoStart)
	{
		ChatId = chatId;
		UserId = userId;
	}

	public int ChatId { get; }
	public long UserId { get; }
}

public class UtilityTimer : Timer
{
	public UtilityTimer(string name, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, null, targetTime, callback,
		ETimerType.Utility, loop, autoStart)
	{
	}

	public UtilityTimer(string name, int hours, int minutes, int seconds, Action<object?, ElapsedEventArgs> callback, ETimerLoop loop = ETimerLoop.No, bool autoStart = true) : base(name, null, hours,
		minutes, seconds, callback, ETimerType.Utility,
		loop, autoStart)
	{
	}
}