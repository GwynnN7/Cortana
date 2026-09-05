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

	/// A sensor that reports presence starts it and keeps it, and the window carries it for a while
	/// after it goes quiet. A sensor that only sustains extends a presence that is already alive but
	/// can never announce one, so a desk woken from somewhere else lights nothing
	public static bool Present(DateTimeOffset? lastMotionAt, DateTimeOffset now, TimeSpan timeout,
		bool reported, bool sustained, bool wasPresent) =>
		reported || MotionActive(lastMotionAt, now, timeout) || (wasPresent && sustained);
}
