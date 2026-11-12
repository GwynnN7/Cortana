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

}
