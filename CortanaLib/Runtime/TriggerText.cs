using System.Globalization;
using CortanaLib.Contracts;

namespace CortanaLib.Runtime;

/// One place that spells a trigger, so every client reads the same sentence
public static class TriggerText
{
	public static string Kind(TriggerKind kind) => kind switch
	{
		TriggerKind.IsTrue => "is true",
		TriggerKind.IsFalse => "is false",
		TriggerKind.Below => "at or below",
		TriggerKind.Above => "at or above",
		TriggerKind.Outside => "outside",
		_ => "changed"
	};

	public static string Describe(Trigger trigger, Func<string, string>? name = null)
	{
		string sensor = name?.Invoke(trigger.Sensor) ?? trigger.Sensor;

		return trigger.Kind switch
		{
			TriggerKind.IsTrue => sensor,
			TriggerKind.IsFalse => $"no {sensor.ToLowerInvariant()}",
			TriggerKind.Below => $"{sensor} ≤ {Number(trigger.Low)}",
			TriggerKind.Above => $"{sensor} ≥ {Number(trigger.High)}",
			TriggerKind.Outside => $"{sensor} outside {Number(trigger.Low)}–{Number(trigger.High)}",
			_ => $"{sensor} changed"
		};
	}

	private static string Number(double? value) =>
		value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "?";
}
