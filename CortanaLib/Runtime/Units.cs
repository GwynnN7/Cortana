using System.Globalization;
using CortanaLib.Primitives;

namespace CortanaLib.Runtime;

/// One place that decides how a reading is spelled, so every client renders it the same way
public static class Units
{
	public static string For(SensorId sensor) => sensor switch
	{
		SensorId.Temperature => "°C",
		SensorId.Humidity => " %",
		SensorId.Light => " lux",
		SensorId.Co2 => " ppm",
		SensorId.Tvoc => " ppb",
		_ => ""
	};

	public static string For(RaspberryInfo info) => info == RaspberryInfo.Temperature ? "°C" : "";

	public static string For(SettingKey setting) => setting switch
	{
		SettingKey.LightThreshold => " lux",
		SettingKey.TemperatureOffset => "°C",
		SettingKey.Co2Threshold => " ppm",
		SettingKey.TvocThreshold => " ppb",
		SettingKey.MorningHour or SettingKey.NightHour => "h",
		SettingKey.MotionTimeoutSeconds or SettingKey.ComputerShutdownGraceSeconds => "s",
		SettingKey.ManualOverrideMinutes or SettingKey.SleepManualOverrideMinutes or SettingKey.SleepHoldMinutes
			or SettingKey.SleepEntryDelayMinutes or SettingKey.DaySleepMinutes => " min",
		_ => ""
	};

	public static string ForMetric(string metric) => metric.ToLowerInvariant() switch
	{
		"temperature" or "pi_temp" or "pc_temp" or "pc_gpu_temp" => "°C",
		"humidity" or "pi_cpu" or "pi_ram" or "pc_cpu" or "pc_ram" or "pc_gpu" => "%",
		"light" => " lux",
		"co2" => " ppm",
		"tvoc" => " ppb",
		_ => ""
	};

	public static string Temperature(double celsius, int digits = 1) => $"{Math.Round(celsius, digits).ToString(CultureInfo.InvariantCulture)}°C";

	public static string Number(double value, int digits = 1) => Math.Round(value, digits).ToString(CultureInfo.InvariantCulture);
}
