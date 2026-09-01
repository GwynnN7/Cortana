using CortanaLib.Contracts;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.History;

/// Deterministic reductions over recorded data
public static class HistoryAnalysis
{
	public static AnalysisResult Run(
		AnalysisFunction function,
		string metric,
		IReadOnlyList<HistoryPoint> points,
		IReadOnlyList<HistoryPoint> comparison,
		DateTimeOffset? at,
		double? state,
		int windowMinutes)
	{
		string unit = Units.ForMetric(metric);

		if (points.Count == 0)
			return new AnalysisResult(function, metric, unit, null, null, 0, $"Nothing was recorded for {metric} in that window");

		return function switch
		{
			AnalysisFunction.Average => Value(function, metric, unit, points.Average(point => point.Value), null, points.Count,
				$"{metric} averaged {Units.Number(points.Average(point => point.Value))}{unit} over {points.Count} samples"),

			AnalysisFunction.Minimum => Extreme(function, metric, unit, points, points.MinBy(point => point.Value)!, "lowest"),

			AnalysisFunction.Maximum => Extreme(function, metric, unit, points, points.MaxBy(point => point.Value)!, "highest"),

			AnalysisFunction.ValueAt => Nearest(function, metric, unit, points, at),

			AnalysisFunction.Trend => Trend(function, metric, unit, points),

			AnalysisFunction.CountTransitions => Transitions(function, metric, points),

			AnalysisFunction.DurationInState => Duration(function, metric, points, state ?? 1),

			AnalysisFunction.WorstPeriod => Worst(function, metric, unit, points, windowMinutes),

			AnalysisFunction.Compare => Compare(function, metric, unit, points, comparison),

			_ => new AnalysisResult(function, metric, unit, null, null, points.Count, "Unsupported analysis")
		};
	}

	private static AnalysisResult Value(AnalysisFunction function, string metric, string unit, double? value, DateTimeOffset? at, int samples, string summary) =>
		new(function, metric, unit, value, at, samples, summary);

	private static AnalysisResult Extreme(AnalysisFunction function, string metric, string unit, IReadOnlyList<HistoryPoint> points, HistoryPoint found, string word) =>
		Value(function, metric, unit, found.Value, found.At, points.Count,
			$"The {word} {metric} was {Units.Number(found.Value)}{unit} at {found.At:dd MMM HH:mm}");

	private static AnalysisResult Nearest(AnalysisFunction function, string metric, string unit, IReadOnlyList<HistoryPoint> points, DateTimeOffset? at)
	{
		if (at is not { } moment)
			return new AnalysisResult(function, metric, unit, null, null, points.Count, "ValueAt needs a moment in time");

		HistoryPoint closest = points.MinBy(point => Math.Abs((point.At - moment).TotalSeconds))!;
		return Value(function, metric, unit, closest.Value, closest.At, points.Count,
			$"{metric} was {Units.Number(closest.Value)}{unit} at {closest.At:dd MMM HH:mm}, the closest sample to {moment:dd MMM HH:mm}");
	}

	/// Difference between the means of the two halves of the window
	private static AnalysisResult Trend(AnalysisFunction function, string metric, string unit, IReadOnlyList<HistoryPoint> points)
	{
		if (points.Count < 4)
			return new AnalysisResult(function, metric, unit, null, null, points.Count, $"Too few samples to read a trend for {metric}");

		int half = points.Count / 2;
		double before = points.Take(half).Average(point => point.Value);
		double after = points.Skip(half).Average(point => point.Value);
		double delta = after - before;

		string direction = Math.Abs(delta) < 0.05 ? "held steady" : delta > 0 ? "rose" : "fell";
		return Value(function, metric, unit, Math.Round(delta, 2), null, points.Count,
			$"{metric} {direction} by {Units.Number(Math.Abs(delta))}{unit} across the window");
	}

	private static AnalysisResult Transitions(AnalysisFunction function, string metric, IReadOnlyList<HistoryPoint> points)
	{
		var count = 0;
		for (var index = 1; index < points.Count; index++)
			if (Math.Abs(points[index].Value - points[index - 1].Value) > 0.5) count++;

		return Value(function, metric, "", count, null, points.Count, $"{metric} changed {count} times");
	}

	/// Sums the gaps between consecutive samples that sit in the requested state
	private static AnalysisResult Duration(AnalysisFunction function, string metric, IReadOnlyList<HistoryPoint> points, double state)
	{
		var total = TimeSpan.Zero;
		for (var index = 1; index < points.Count; index++)
		{
			if (Math.Abs(points[index - 1].Value - state) > 0.5) continue;

			TimeSpan gap = points[index].At - points[index - 1].At;
			if (gap > TimeSpan.Zero && gap < TimeSpan.FromHours(1)) total += gap;
		}

		return Value(function, metric, "", Math.Round(total.TotalMinutes, 1), null, points.Count,
			$"{metric} spent {total.TotalMinutes:0} minutes at {Units.Number(state)}");
	}

	/// The window whose average is highest, which is what "worst air quality period" means
	private static AnalysisResult Worst(AnalysisFunction function, string metric, string unit, IReadOnlyList<HistoryPoint> points, int windowMinutes)
	{
		var window = TimeSpan.FromMinutes(Math.Clamp(windowMinutes, 5, 24 * 60));
		double worstAverage = double.MinValue;
		DateTimeOffset worstAt = points[0].At;

		for (var start = 0; start < points.Count; start++)
		{
			DateTimeOffset until = points[start].At + window;
			List<double> inside = [.. points.Skip(start).TakeWhile(point => point.At <= until).Select(point => point.Value)];
			if (inside.Count == 0) continue;

			double average = inside.Average();
			if (average <= worstAverage) continue;

			worstAverage = average;
			worstAt = points[start].At;
		}

		return Value(function, metric, unit, Math.Round(worstAverage, 1), worstAt, points.Count,
			$"The worst {window.TotalMinutes:0} minute window for {metric} started at {worstAt:dd MMM HH:mm} averaging {Units.Number(worstAverage)}{unit}");
	}

	private static AnalysisResult Compare(AnalysisFunction function, string metric, string unit, IReadOnlyList<HistoryPoint> points, IReadOnlyList<HistoryPoint> comparison)
	{
		if (comparison.Count == 0)
			return new AnalysisResult(function, metric, unit, null, null, points.Count, "Nothing was recorded in the comparison window");

		double first = points.Average(point => point.Value);
		double second = comparison.Average(point => point.Value);
		double delta = first - second;

		return Value(function, metric, unit, Math.Round(delta, 2), null, points.Count + comparison.Count,
			$"{metric} averaged {Units.Number(first)}{unit} against {Units.Number(second)}{unit}, a difference of {Units.Number(delta)}{unit}");
	}
}
