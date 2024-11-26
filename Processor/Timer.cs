global using Times = (int Seconds, int Minutes, int Hours);
using System.Timers;

namespace Processor;

public record TimerArg<T>(T? Arg);
public record TelegramTimerPayload<T>(long ChatId, long UserId, T? Arg) : TimerArg<T>(Arg);
public record DiscordTimerPayload<T>(object User, object? TextChannel, T? Arg) : TimerArg<T>(Arg);

public class Timer : System.Timers.Timer
{
	private static readonly Dictionary<ETimerType, List<Timer>> TotalTimers = new();
	public DateTime NextTargetTime { get; private set; }
	public ETimerType TimerType { get; }
	public object? Payload { get; }
	private string Tag { get; }
	private ETimerLoop LoopType { get; }
	
	private Action<object?, ElapsedEventArgs> Callback { get; }
	
	public Timer(string tag, object? payload, Times times, Action<object?, ElapsedEventArgs> callback, ETimerType timerType, ETimerLoop loop = ETimerLoop.No, bool autoStart = true)
	{
		Tag = tag;
		Payload = payload;
		Interval = (times.Hours * 3600 + times.Minutes * 60 + times.Seconds) * 1000;
		Callback = callback;
		TimerType = timerType;

		LoopType = loop;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

		Elapsed += TimerElapsed;
		AutoReset = false;
		if (autoStart) Start();

		SaveTimer();
	}

	public Timer(string tag, object? payload, DateTime targetTime, Action<object?, ElapsedEventArgs> callback, ETimerType timerType, ETimerLoop loop = ETimerLoop.No, bool autoStart = true)
	{
		Tag = tag;
		Payload = payload;
		Interval = targetTime.Subtract(DateTime.Now).TotalMilliseconds;
		if(Interval <= 0) Interval = 1000;
		Callback = callback;
		TimerType = timerType;

		LoopType = loop;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

		Elapsed += TimerElapsed;
		AutoReset = false;
		if (autoStart) Start();

		SaveTimer();
	}

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

	public static void RemoveTimerByTag(string tag)
	{
		foreach ((_, List<Timer>? timerHandlers) in TotalTimers)
		{
			foreach (Timer listTimer in timerHandlers.Where(listTimer => listTimer.Tag == tag))
			{
				RemoveTimer(listTimer);
				return;
			}
		}
	}

	public static List<Timer> GetDiscordTimers()
	{
		return TotalTimers[ETimerType.Discord];
	}

	public static List<Timer> GetTelegramTimers()
	{
		return TotalTimers[ETimerType.Telegram];
	}

	public static List<Timer> GetUtilityTimers()
	{
		return TotalTimers[ETimerType.Utility];
	}
}