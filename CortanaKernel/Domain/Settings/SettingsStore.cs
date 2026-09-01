using System.Globalization;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Settings;

public interface ISettingsRepository
{
	IReadOnlyDictionary<SettingKey, string> Load();
	void Save(IReadOnlyDictionary<SettingKey, string> values);
}

public sealed record SettingDefinition(SettingKey Key, string Default, double Min, double Max, bool IsFlag = false, int Decimals = 0);

/// Domain-owned runtime settings
public sealed class SettingsStore(ISettingsRepository repository)
{
	private static readonly IReadOnlyDictionary<SettingKey, SettingDefinition> Definitions =
		new SettingDefinition[]
		{
			new(SettingKey.AutomationEnabled, "On", 0, 1, IsFlag: true),
			new(SettingKey.LightThreshold, "60", 0, 65535),
			new(SettingKey.Co2Threshold, "1000", 0, 10000),
			new(SettingKey.TvocThreshold, "250", 0, 10000),
			new(SettingKey.MorningHour, "9", 0, 23),
			new(SettingKey.NightHour, "23", 0, 23),
			new(SettingKey.TemperatureOffset, "0", -10, 10, Decimals: 1),
			new(SettingKey.MotionTimeoutSeconds, "30", 1, 3600),
			new(SettingKey.ManualOverrideMinutes, "15", 1, 720),
			new(SettingKey.SleepManualOverrideMinutes, "10", 1, 720),
			new(SettingKey.SleepHoldMinutes, "60", 1, 720),
			new(SettingKey.SleepEntryDelayMinutes, "10", 0, 720),
			new(SettingKey.DaySleepMinutes, "90", 1, 1440),
			new(SettingKey.ComputerShutdownGraceSeconds, "20", 0, 600),
			new(SettingKey.LampUsesPulseRelay, "Off", 0, 1, IsFlag: true),
			new(SettingKey.NotifyWeb, "On", 0, 1, IsFlag: true),
			new(SettingKey.NotifyTelegram, "Off", 0, 1, IsFlag: true),
			new(SettingKey.NotifyDiscord, "Off", 0, 1, IsFlag: true)
		}.ToDictionary(definition => definition.Key);

	private readonly Dictionary<SettingKey, string> _values = Initialise(repository);
	private readonly Lock _gate = new();

	public event Action<SettingKey, string>? Changed;

	private static Dictionary<SettingKey, string> Initialise(ISettingsRepository repository)
	{
		Dictionary<SettingKey, string> values = Definitions.ToDictionary(entry => entry.Key, entry => entry.Value.Default);
		foreach ((SettingKey key, string value) in repository.Load())
			if (Definitions.ContainsKey(key) && Normalise(key, value) is { } normalised)
				values[key] = normalised;

		return values;
	}

	public string Read(SettingKey key)
	{
		lock (_gate) return _values[key];
	}

	public bool Flag(SettingKey key) => Read(key).Equals(nameof(PowerState.On), StringComparison.OrdinalIgnoreCase);

	public int Number(SettingKey key) => int.TryParse(Read(key), CultureInfo.InvariantCulture, out int value) ? value : 0;

	public double Decimal(SettingKey key) => double.TryParse(Read(key), CultureInfo.InvariantCulture, out double value) ? value : 0;

	public TimeSpan Minutes(SettingKey key) => TimeSpan.FromMinutes(Number(key));

	public TimeSpan Seconds(SettingKey key) => TimeSpan.FromSeconds(Number(key));

	public IReadOnlyList<SettingView> All()
	{
		lock (_gate)
			return [.. Definitions.Keys.Select(key => new SettingView(key, _values[key], Units.For(key)))];
	}

	public SettingView View(SettingKey key) => new(key, Read(key), Units.For(key));

	/// Accepts "on"/"off"/"toggle" for flags and a number for the rest
	public Result<string> Write(SettingKey key, string requested)
	{
		if (!Definitions.TryGetValue(key, out SettingDefinition? definition)) return Result.Fail<string>($"Unknown setting '{key}'");

		string? normalised = Normalise(key, requested, Read(key));
		if (normalised == null)
			return Result.Fail<string>(definition.IsFlag
				? $"{key} accepts On, Off or Toggle"
				: $"{key} must be a number between {definition.Min:0.##} and {definition.Max:0.##}");

		lock (_gate)
		{
			if (_values[key] == normalised) return Result.Ok(normalised);
			_values[key] = normalised;
			repository.Save(_values);
		}

		Changed?.Invoke(key, normalised);
		return Result.Ok(normalised);
	}

	private static string? Normalise(SettingKey key, string requested, string? current = null)
	{
		SettingDefinition definition = Definitions[key];
		string trimmed = requested.Trim();

		if (definition.IsFlag)
		{
			if (trimmed.Equals("toggle", StringComparison.OrdinalIgnoreCase))
				return current == nameof(PowerState.On) ? nameof(PowerState.Off) : nameof(PowerState.On);

			if (trimmed.Equals("on", StringComparison.OrdinalIgnoreCase) || trimmed == "1") return nameof(PowerState.On);
			if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) || trimmed == "0") return nameof(PowerState.Off);
			return null;
		}

		if (!double.TryParse(trimmed, CultureInfo.InvariantCulture, out double number)) return null;
		if (number < definition.Min || number > definition.Max) return null;

		return Math.Round(number, definition.Decimals).ToString($"0.{new string('#', definition.Decimals)}", CultureInfo.InvariantCulture);
	}
}
