using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Fabric;

public readonly record struct BindDecision(PowerState? Target, string Reason, bool Suspended = false);

public static class BindRules
{
	public static bool Holds(Trigger trigger, Reading? reading) =>
		reading is not null && trigger.Kind switch
		{
			TriggerKind.IsTrue => reading.Value >= 0.5,
			TriggerKind.IsFalse => reading.Value < 0.5,
			TriggerKind.Below => trigger.Low is { } low && reading.Value <= low,
			TriggerKind.Above => trigger.High is { } high && reading.Value >= high,
			TriggerKind.Outside => (trigger.Low is { } min && reading.Value < min) || (trigger.High is { } max && reading.Value > max),
			_ => true
		};

	public static BindDecision Decide(Bind bind, bool isOn, Func<string, Reading?> read)
	{
		if (!bind.Enabled) return new BindDecision(null, $"{bind.Id} is disabled");
		if (bind.Triggers.Count == 0) return new BindDecision(null, $"{bind.Id} has no triggers");

		string[] blind = [.. bind.Triggers.Where(trigger => read(trigger.Sensor) is null).Select(trigger => trigger.Sensor)];

		if (blind.Length > 0)
			return new BindDecision(null, $"waiting on {string.Join(" and ", blind.Distinct())}", Suspended: true);

		Trigger[] sustaining = [.. bind.Triggers.Where(trigger => trigger.Sustains)];

		if (sustaining.Length > 0 && !Satisfied(sustaining, bind.Mode, read))
			return new BindDecision(PowerState.Off, Describe(sustaining, bind.Mode, read, holds: false));

		if (isOn) return new BindDecision(PowerState.On, "it is already on and still held");

		return Satisfied(bind.Triggers, bind.Mode, read)
			? new BindDecision(PowerState.On, Describe(bind.Triggers, bind.Mode, read, holds: true))
			: new BindDecision(null, "not every condition to switch it on is met");
	}

	private static bool Satisfied(IReadOnlyList<Trigger> triggers, BindMode mode, Func<string, Reading?> read) =>
		mode == BindMode.All
			? triggers.All(trigger => Holds(trigger, read(trigger.Sensor)))
			: triggers.Any(trigger => Holds(trigger, read(trigger.Sensor)));

	private static string Describe(IReadOnlyList<Trigger> triggers, BindMode mode, Func<string, Reading?> read, bool holds)
	{
		IEnumerable<Trigger> relevant = holds
			? triggers.Where(trigger => Holds(trigger, read(trigger.Sensor)))
			: triggers.Where(trigger => !Holds(trigger, read(trigger.Sensor)));

		string[] parts = [.. relevant.Select(trigger => Phrase(trigger, read(trigger.Sensor)))];
		if (parts.Length == 0) return holds ? "every condition is met" : "no condition is met";

		return string.Join(mode == BindMode.All && holds ? " and " : ", ", parts);
	}

	private static string Phrase(Trigger trigger, Reading? reading)
	{
		string value = reading is null ? "no reading" : Units.Number(reading.Value);

		return trigger.Kind switch
		{
			TriggerKind.IsTrue => reading is { Value: >= 0.5 } ? $"{trigger.Sensor} is active" : $"{trigger.Sensor} is not active",
			TriggerKind.IsFalse => reading is { Value: < 0.5 } ? $"{trigger.Sensor} is clear" : $"{trigger.Sensor} is not clear",
			TriggerKind.Below => $"{trigger.Sensor} at {value} against a {Units.Number(trigger.Low ?? 0)} floor",
			TriggerKind.Above => $"{trigger.Sensor} at {value} against a {Units.Number(trigger.High ?? 0)} ceiling",
			TriggerKind.Outside => $"{trigger.Sensor} at {value} is outside its range",
			_ => $"{trigger.Sensor} changed"
		};
	}
}
