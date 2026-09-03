using CortanaLib.Contracts;

namespace CortanaKernel.Domain.History;

public interface IRhythmRepository
{
	IReadOnlyList<DaySummary> Load(int days);
	void Save(DaySummary day);
}

/// What a day looked like, reduced to the few numbers a rhythm is made of
public static class DayRhythm
{
	/// When it *became* true, not when it was first seen true. A day that starts with the computer
	/// already on has no "came on" moment, and pretending it happened at 00:00 poisons the median
	public static int? Rose(IReadOnlyList<HistoryPoint> points)
	{
		for (var i = 1; i < points.Count; i++)
			if (points[i].Value >= 0.5 && points[i - 1].Value < 0.5) return Minute(points[i].At);

		return null;
	}

	public static int? Fell(IReadOnlyList<HistoryPoint> points)
	{
		for (int i = points.Count - 1; i > 0; i--)
			if (points[i].Value < 0.5 && points[i - 1].Value >= 0.5) return Minute(points[i].At);

		return null;
	}

	public static double Minutes(IReadOnlyList<HistoryPoint> points, double every) =>
		Math.Round(points.Count(point => point.Value >= 0.5) * every, 1);

	private static int Minute(DateTimeOffset at) => at.LocalDateTime.Hour * 60 + at.LocalDateTime.Minute;

	/// The median of a set of minute-of-day readings, which one late night cannot move
	public static int? Usual(IEnumerable<int?> minutes)
	{
		int[] known = [.. minutes.Where(minute => minute.HasValue).Select(minute => minute!.Value).Order()];
		return known.Length == 0 ? null : known[known.Length / 2];
	}

	public static string Spell(int? minute) =>
		minute is { } value ? $"{value / 60:00}:{value % 60:00}" : "never";
}
