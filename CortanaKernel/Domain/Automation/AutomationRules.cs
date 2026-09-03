using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

public static class AutomationRules
{
	public static TimeContext ContextAt(DateTimeOffset now, int nightHour, int morningHour)
	{
		if (nightHour == morningHour) return TimeContext.Day;

		int hour = now.Hour;
		bool night = nightHour < morningHour
			? hour >= nightHour && hour < morningHour
			: hour >= nightHour || hour < morningHour;

		return night ? TimeContext.Night : TimeContext.Day;
	}

	public static bool MotionActive(DateTimeOffset? lastMotionAt, DateTimeOffset now, TimeSpan timeout) =>
		lastMotionAt.HasValue && now - lastMotionAt.Value < timeout;

	public static bool Present(DateTimeOffset? lastMotionAt, DateTimeOffset now, TimeSpan timeout, bool live) =>
		live || MotionActive(lastMotionAt, now, timeout);
}
