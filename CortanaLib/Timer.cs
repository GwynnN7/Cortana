global using Times = (int Seconds, int Minutes, int Hours);
using Utility.Structures;

namespace Utility;

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
	private Func<object?, Task> Callback { get; }
	
	
	public Timer(string tag, object? payload, Func<object?, Task> callback, ETimerType timerType, ETimerLoop loop = ETimerLoop.No)
	{
		Tag = tag;
		Payload = payload;
		Callback = callback;
		TimerType = timerType;
		LoopType = loop;
		AutoReset = false;
		
		Elapsed += (sender, _) => Task.Run(async () => await TimerElapsed(sender));
		SaveTimer();
	}
	
	public void Set(Times times)
	{
		double interval = (times.Hours * 3600 + times.Minutes * 60 + times.Seconds) * 1000;
		Interval = interval > 0 ? interval : 1000;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);
		
		Start();
	}
	
	public void Set(DateTime targetTime)
	{
		if(targetTime <= DateTime.Now) targetTime = targetTime.AddDays(1);
		Interval = targetTime.Subtract(DateTime.Now).TotalMilliseconds;
		NextTargetTime = DateTime.Now.AddMilliseconds(Interval);
		
		Start();
	}

	private async Task TimerElapsed(object? sender)
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

		await Callback.Invoke(sender);

		if (LoopType == ETimerLoop.No) RemoveTimer(this);
	}

	public void Destroy()
	{
		Stop();
		Close();
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