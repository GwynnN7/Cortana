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

	public static TimeSpan MotionTimeout(bool computerConnected, TimeSpan computerOn, TimeSpan computerOff) =>
		computerConnected ? computerOn : computerOff;

	public static LampDecision DecideLamp(LampInput input)
	{
		if (!input.AutomationEnabled) return new LampDecision(null, "automation is off");
		if (input.OverrideActive) return new LampDecision(null, "a manual override is active");
		if (input.SleepMode) return new LampDecision(PowerState.Off, "sleep mode keeps the lamp off");

		if (!input.MotionActive) return new LampDecision(PowerState.Off, "no motion within the timeout");
		if (input.LampIsOn) return new LampDecision(PowerState.On, "motion, the lamp stays on");
		if (input.Light is not { } light) return new LampDecision(null, "motion, but no light reading to judge by");

		return light <= input.LightThreshold
			? new LampDecision(PowerState.On, $"motion and {light} lux is at or below the {input.LightThreshold} lux threshold")
			: new LampDecision(null, $"motion, but {light} lux is above the {input.LightThreshold} lux threshold");
	}

	public static bool AirQualityUnsafe(int co2, int tvoc, int co2Threshold, int tvocThreshold) =>
		tvoc >= tvocThreshold * 1.15 || co2 >= co2Threshold * 1.15;

	public static bool AirQualityBackToNormal(int co2, int tvoc, int co2Threshold, int tvocThreshold) =>
		tvoc < tvocThreshold * 0.9 && co2 < co2Threshold * 0.9;
}

public readonly record struct LampInput(
	bool AutomationEnabled,
	bool OverrideActive,
	bool SleepMode,
	bool MotionActive,
	bool LampIsOn,
	int? Light,
	int LightThreshold);

public readonly record struct LampDecision(PowerState? Target, string Reason);
