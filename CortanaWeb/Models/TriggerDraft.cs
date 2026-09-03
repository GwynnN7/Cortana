using System.Globalization;
using CortanaLib.Contracts;

namespace CortanaWeb.Models;

/// A trigger being typed, kept as text until it is read back
public sealed class TriggerDraft
{
	public string Sensor { get; set; } = "";
	public TriggerKind Kind { get; set; } = TriggerKind.IsTrue;
	public string Low { get; set; } = "";
	public string High { get; set; } = "";
	public bool Sustains { get; set; } = true;

	public Trigger Read() => new(Sensor, Kind,
		Kind is TriggerKind.Below or TriggerKind.Outside ? Drafts.Number(Low) : null,
		Kind == TriggerKind.Above ? Drafts.Number(Low) : Kind == TriggerKind.Outside ? Drafts.Number(High) : null,
		Sustains);

	public void Clear()
	{
		Low = "";
		High = "";
	}
}

/// What a typed form has to turn back into a value
public static class Drafts
{
	public static double? Number(string value) =>
		double.TryParse(value, CultureInfo.InvariantCulture, out double parsed) ? parsed : null;

	public static string Slug(string name) =>
		new(name.Trim().ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}
