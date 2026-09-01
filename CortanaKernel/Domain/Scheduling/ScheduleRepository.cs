using CortanaLib.Contracts;

namespace CortanaKernel.Domain.Scheduling;

public interface IScheduleRepository
{
	IReadOnlyList<Schedule> Load();
	void Save(IReadOnlyList<Schedule> schedules);
}

/// When a schedule should next run
public static class ScheduleTiming
{
	public static DateTimeOffset? NextRun(Schedule schedule, DateTimeOffset now)
	{
		if (!schedule.Enabled) return null;

		switch (schedule.Trigger)
		{
			case ScheduleTrigger.Once:
				return schedule.LastRun != null ? null : schedule.At;

			case ScheduleTrigger.Interval:
				if (schedule.IntervalSeconds <= 0) return null;
				DateTimeOffset anchor = schedule.LastRun ?? schedule.CreatedAt;
				DateTimeOffset next = anchor.AddSeconds(schedule.IntervalSeconds);
				if (next >= now) return next;

				long skipped = (long)((now - anchor).TotalSeconds / schedule.IntervalSeconds);
				return anchor.AddSeconds(skipped * schedule.IntervalSeconds);

			case ScheduleTrigger.Daily:
				return NextAt(now, schedule.Hour, schedule.Minute, null);

			case ScheduleTrigger.Weekly:
				return NextAt(now, schedule.Hour, schedule.Minute, schedule.Day);

			default:
				return null;
		}
	}

	/// Whether an event-triggered schedule should run
	public static bool ShouldFireOnEvent(Schedule schedule, ScheduleEvent raised, DateTimeOffset now)
	{
		if (!schedule.Enabled || schedule.Trigger != ScheduleTrigger.Event || schedule.Event != raised) return false;

		if (schedule.RunOnce && schedule.LastRun != null) return false;

		if (schedule.MinimumIntervalSeconds > 0 && schedule.LastRun is { } last &&
			now - last < TimeSpan.FromSeconds(schedule.MinimumIntervalSeconds)) return false;

		return true;
	}

	private static DateTimeOffset NextAt(DateTimeOffset now, int hour, int minute, DayOfWeek? day)
	{
		var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
		if (candidate <= now) candidate = candidate.AddDays(1);

		if (day == null) return candidate;

		while (candidate.DayOfWeek != day.Value) candidate = candidate.AddDays(1);
		return candidate;
	}
}
