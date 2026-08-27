global using Times = (int Seconds, int Minutes, int Hours);

namespace CortanaLib.Structures;

public record TimerArg<T>(T? Arg);
public record TelegramTimerPayload<T>(long ChatId, T? Arg) : TimerArg<T>(Arg);
public record DiscordTimerPayload<T>(object User, object? TextChannel, T? Arg) : TimerArg<T>(Arg);

public class Timer : System.Timers.Timer
{
	private static readonly Lock TimerLock = new();
	private static readonly Dictionary<ETimerType, List<Timer>> TotalTimers = new();

	public DateTime NextTargetTime { get; private set; }
	public ETimerType TimerType { get; }
	public object? Payload { get; }
	public string Tag { get; }
	private ETimerLoop LoopType { get; }
	private Func<object?, Task> Callback { get; }
	private double _period;

	public Timer(string tag, object? payload, Func<object?, Task> callback, ETimerType timerType, ETimerLoop loop = ETimerLoop.No)
	{
		Tag = tag;
		Payload = payload;
		Callback = callback;
		TimerType = timerType;
		LoopType = loop;
		AutoReset = false;

		Elapsed += (sender, args) => Task.Run(() => TimerElapsed(sender));
		SaveTimer();
	}

	public Timer Set(Times times)
	{
		double interval = (times.Hours * 3600 + times.Minutes * 60 + times.Seconds) * 1000;
		_period = interval > 0 ? interval : 1000;
		Interval = _period;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);

		Start();
		return this;
	}

	public Timer Set(DateTime targetTime)
	{
		if (targetTime <= DateTime.Now) targetTime = targetTime.AddDays(1);
		Interval = targetTime.Subtract(DateTime.Now).TotalMilliseconds;
		_period = Interval;
		NextTargetTime = targetTime;

		Start();
		return this;
	}

	private async Task TimerElapsed(object? sender)
	{
		if (LoopType != ETimerLoop.No) Reschedule();

		try
		{
			await Callback.Invoke(sender);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[Timer:{Tag}] callback failed: {ex.Message}");
		}

		if (LoopType == ETimerLoop.No) Destroy();
	}

		private void Reschedule()
	{
		DateTime anchor = NextTargetTime == default ? DateTime.Now : NextTargetTime;
		DateTime next = LoopType switch
		{
			ETimerLoop.Daily => anchor.AddDays(1),
			ETimerLoop.Weekly => anchor.AddDays(7),
			_ => anchor.AddMilliseconds(_period)
		};
		while (next <= DateTime.Now)
		{
			next = LoopType switch
			{
				ETimerLoop.Daily => next.AddDays(1),
				ETimerLoop.Weekly => next.AddDays(7),
				_ => next.AddMilliseconds(_period)
			};
		}

		NextTargetTime = next;
		Interval = Math.Max(1, next.Subtract(DateTime.Now).TotalMilliseconds);
		Start();
	}

	public void Destroy()
	{
		Stop();
		lock (TimerLock)
		{
			foreach ((_, List<Timer> timerList) in TotalTimers)
			{
				if (timerList.Remove(this)) break;
			}
		}
		Close();
	}

	private void SaveTimer()
	{
		lock (TimerLock)
		{
			if (!TotalTimers.TryAdd(TimerType, [this])) TotalTimers[TimerType].Add(this);
		}
	}

	public static void RemoveTimer(Timer timer) => timer.Destroy();

	public static void RemoveTimers(ETimerType timerType)
	{
		List<Timer> snapshot;
		lock (TimerLock)
		{
			if (!TotalTimers.TryGetValue(timerType, out List<Timer>? timers)) return;
			snapshot = [.. timers];
		}
		foreach (Timer timer in snapshot) timer.Destroy();
	}

	public static void RemoveTimerByTag(string tag)
	{
		Timer? found;
		lock (TimerLock)
		{
			found = TotalTimers.Values.SelectMany(list => list).FirstOrDefault(t => t.Tag == tag);
		}
		found?.Destroy();
	}

	public static List<Timer> GetTimers(ETimerType timerType)
	{
		lock (TimerLock)
		{
			return TotalTimers.TryGetValue(timerType, out List<Timer>? timers) ? [.. timers] : [];
		}
	}

	public static List<Timer> GetDiscordTimers() => GetTimers(ETimerType.Discord);
	public static List<Timer> GetTelegramTimers() => GetTimers(ETimerType.Telegram);
	public static List<Timer> GetUtilityTimers() => GetTimers(ETimerType.Utility);
}
