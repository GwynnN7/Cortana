using System.Globalization;
using CortanaLib.Primitives;

namespace CortanaLib.Runtime;

/// One place that decides how a reading is spelled, so every client renders it the same way
public static class Units
{
	public static string For(RaspberryInfo info) => info == RaspberryInfo.Temperature ? "°C" : "";

	public static string For(SettingKey setting) => setting switch
	{
		SettingKey.MorningHour or SettingKey.NightHour => "h",
		SettingKey.MotionTimeoutSeconds or SettingKey.ComputerShutdownGraceSeconds => "s",
		SettingKey.ManualOverrideMinutes or SettingKey.SleepManualOverrideMinutes or SettingKey.SleepHoldMinutes
			or SettingKey.SleepEntryDelayMinutes or SettingKey.DaySleepMinutes => " min",
		_ => ""
	};

	public static string Temperature(double celsius, int digits = 1) => $"{Math.Round(celsius, digits).ToString(CultureInfo.InvariantCulture)}°C";

	public static string Number(double value, int digits = 1) => Math.Round(value, digits).ToString(CultureInfo.InvariantCulture);

	public static string Elapsed(TimeSpan span) => span switch
	{
		{ TotalDays: >= 1 } => $"{(int)span.TotalDays}d {span.Hours}h",
		{ TotalHours: >= 1 } => $"{(int)span.TotalHours}h {span.Minutes}m",
		_ => $"{(int)span.TotalMinutes}m"
	};
}
