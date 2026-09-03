using CortanaLib.Contracts;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.History;

public static class HistoryBaseline
{
	private const double MadToSigma = 1.4826;
	private const int LeastSamples = 8;

	public static BaselineResult Build(string metric, string unit, IReadOnlyList<HistoryPoint> points, double? current, int hour)
	{
		double[] values = [.. points.Where(point => point.At.Hour == hour).Select(point => point.Value).Order()];

		if (values.Length < LeastSamples)
			return new BaselineResult(metric, unit, hour, null, null, null, null, values.Length,
				$"There is not enough history for {metric} around {hour:00}:00 yet, only {values.Length} samples");

		double median = Median(values);
		double spread = Median([.. values.Select(value => Math.Abs(value - median)).Order()]) * MadToSigma;

		if (current is not { } reading)
			return new BaselineResult(metric, unit, hour, median, spread, null, null, values.Length,
				$"Around {hour:00}:00, {metric} is usually {Units.Number(median)}{unit}");

		double deviation = spread > 0 ? (reading - median) / spread : 0;

		return new BaselineResult(metric, unit, hour, median, spread, reading, deviation, values.Length,
			Describe(metric, unit, hour, median, reading, deviation, values.Length));
	}

	private static string Describe(string metric, string unit, int hour, double median, double reading, double deviation, int samples)
	{
		string usual = $"usually {Units.Number(median)}{unit} around {hour:00}:00, from {samples} samples";
		string now = $"{metric} is {Units.Number(reading)}{unit}";

		return Math.Abs(deviation) switch
		{
			< 1 => $"{now}, which is normal — {usual}",
			< 2 => $"{now}, a little {Direction(deviation)} than usual — {usual}",
			< 3 => $"{now}, clearly {Direction(deviation)} than usual — {usual}",
			_ => $"{now}, far {Direction(deviation)} than anything usual — {usual}"
		};
	}

	private static string Direction(double deviation) => deviation > 0 ? "higher" : "lower";

	private static double Median(double[] ordered) =>
		ordered.Length % 2 == 1
			? ordered[ordered.Length / 2]
			: (ordered[ordered.Length / 2 - 1] + ordered[ordered.Length / 2]) / 2;
}
