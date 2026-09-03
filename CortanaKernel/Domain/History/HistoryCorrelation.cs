using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.History;

public static class HistoryCorrelation
{
	private const int LeastSamples = 10;

	public static CorrelationResult Correlate(string metric, string against,
		IReadOnlyList<HistoryPoint> left, IReadOnlyList<HistoryPoint> right)
	{
		Dictionary<DateTimeOffset, double> paired = right.GroupBy(point => point.At)
			.ToDictionary(group => group.Key, group => group.First().Value);

		var xs = new List<double>();
		var ys = new List<double>();

		foreach (HistoryPoint point in left)
			if (paired.TryGetValue(point.At, out double other))
			{
				xs.Add(point.Value);
				ys.Add(other);
			}

		if (xs.Count < LeastSamples)
			return new CorrelationResult(metric, against, xs.Count, null,
				$"Only {xs.Count} samples line up for {metric} and {against}, not enough to say anything");

		double? coefficient = Pearson(xs, ys);

		if (coefficient is not { } r)
			return new CorrelationResult(metric, against, xs.Count, null,
				$"{metric} or {against} never varied across those {xs.Count} samples, so they cannot be compared");

		return new CorrelationResult(metric, against, xs.Count, Math.Round(r, 2),
			$"{metric} and {against} {Strength(r)} across {xs.Count} samples (r={r:F2})");
	}

	public static CorrelationResult Split(string metric, string unit, string against, double state,
		IReadOnlyList<HistoryPoint> values, IReadOnlyList<HistoryPoint> states, string stateName)
	{
		Dictionary<DateTimeOffset, double> when = states.GroupBy(point => point.At)
			.ToDictionary(group => group.Key, group => group.First().Value);

		var inside = new List<double>();
		var outside = new List<double>();

		foreach (HistoryPoint point in values)
			if (when.TryGetValue(point.At, out double flag))
				(Math.Abs(flag - state) < 0.5 ? inside : outside).Add(point.Value);

		if (inside.Count < 3)
			return new CorrelationResult(metric, against, inside.Count, null,
				$"Only {inside.Count} samples of {metric} while {stateName}, not enough to compare");

		double during = inside.Average();

		if (outside.Count < 3)
			return new CorrelationResult(metric, against, inside.Count, null,
				$"{metric} averages {Units.Number(during)}{unit} while {stateName}, but there is nothing to compare it against yet");

		double other = outside.Average();
		double delta = during - other;

		return new CorrelationResult(metric, against, inside.Count + outside.Count, Math.Round(delta, 1),
			$"{metric} averages {Units.Number(during)}{unit} while {stateName} against {Units.Number(other)}{unit} otherwise, " +
			$"{Units.Number(Math.Abs(delta))}{unit} {(delta >= 0 ? "higher" : "lower")} ({inside.Count} vs {outside.Count} samples)");
	}

	public static SessionInsight Session(string metric, string unit, ActivityCategory category,
		DateTimeOffset since, IReadOnlyList<HistoryPoint> values)
	{
		TimeSpan length = DateTimeOffset.Now - since;

		if (category is ActivityCategory.Idle or ActivityCategory.Away or ActivityCategory.Locked)
			return new SessionInsight(category, since, length, metric, null, null, null, "");

		List<HistoryPoint> inside = [.. values.Where(point => point.At >= since)];

		if (inside.Count < 2)
			return new SessionInsight(category, since, length, metric, null, null, null,
				$"{category.ToString().ToLowerInvariant()} for {Spell(length)}, too early to say what it is doing to {metric}");

		double start = inside[0].Value;
		double latest = inside[^1].Value;
		double delta = latest - start;

		string trend = Math.Abs(delta) < 0.5
			? $"{metric} has not moved from {Units.Number(latest)}{unit}"
			: $"{metric} is {Units.Number(Math.Abs(delta))}{unit} {(delta > 0 ? "up" : "down")}, now {Units.Number(latest)}{unit}";

		return new SessionInsight(category, since, length, metric, start, latest, Math.Round(delta, 1),
			$"{Spell(length)} into {category.ToString().ToLowerInvariant()} and {trend}");
	}

	private static double? Pearson(List<double> xs, List<double> ys)
	{
		double meanX = xs.Average();
		double meanY = ys.Average();

		double covariance = 0, varianceX = 0, varianceY = 0;

		for (var i = 0; i < xs.Count; i++)
		{
			double dx = xs[i] - meanX;
			double dy = ys[i] - meanY;

			covariance += dx * dy;
			varianceX += dx * dx;
			varianceY += dy * dy;
		}

		if (varianceX <= 0 || varianceY <= 0) return null;

		return covariance / Math.Sqrt(varianceX * varianceY);
	}

	private static string Strength(double r) => Math.Abs(r) switch
	{
		< 0.2 => "move independently",
		< 0.4 => r > 0 ? "drift up together, weakly" : "drift apart, weakly",
		< 0.6 => r > 0 ? "clearly rise together" : "clearly move against each other",
		_ => r > 0 ? "track each other closely" : "track each other closely in opposite directions"
	};

	private static string Spell(TimeSpan length) =>
		length.TotalHours >= 1
			? $"{(int)length.TotalHours}h {length.Minutes:00}m"
			: $"{(int)length.TotalMinutes} minutes";
}
