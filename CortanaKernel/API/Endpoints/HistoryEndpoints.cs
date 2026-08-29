using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class HistoryEndpoints
{
	private const int MaxSamples = 240;

	public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.History}").WithTags("History");

		group.MapGet("", Info)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetHistoryInfo")
			.WithSummary("Which metrics are recorded and how often")
			.Produces<HistoryInfoResponse>();

		group.MapGet("/{metric}", Series)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetHistorySeries")
			.WithSummary("Recorded values for one metric over the last hours")
			.Produces<HistorySeries>();
	}

	private static IResult Info(HttpRequest request)
	{
		var response = new HistoryInfoResponse(HistoryService.Metrics, AiSettings.HistoryDays, AiSettings.HistoryMinutes, HistoryService.DiskUsage());
		string text = $"Recording {string.Join(", ", response.Metrics)}\nEvery {response.Minutes} min, kept {response.Days} days, using {response.Bytes / 1024} KB";

		return ApiResults.Ok(request, text, response);
	}

	private static IResult Series(string metric, int? hours, HttpRequest request)
	{
		if (!HistoryService.Metrics.Contains(metric.ToLowerInvariant()))
			return ApiResults.NotFound(request, $"Metric '{metric}' not found. Valid values: {string.Join(", ", HistoryService.Metrics)}");

		DateTime to = DateTime.Now;
		DateTime from = to.AddHours(-Math.Clamp(hours ?? 24, 1, 24 * 365));

		IReadOnlyList<HistoryPoint> points = HistoryService.Read(metric, from, to);
		if (points.Count == 0) return ApiResults.NotFound(request, $"Nothing recorded for '{metric}' in that window");

		var series = new HistorySeries(
			metric.ToLowerInvariant(),
			Unit(metric),
			points.Count,
			Math.Round(points.Min(point => point.Value), 1),
			Math.Round(points.Max(point => point.Value), 1),
			Math.Round(points.Average(point => point.Value), 1),
			Thin(points));

		string text = $"{series.Metric} over {(to - from).TotalHours:F0}h: " +
			$"min {series.Min}{series.Unit}, max {series.Max}{series.Unit}, average {series.Average}{series.Unit} ({series.Points} samples)";

		return ApiResults.Ok(request, text, series);
	}

	private static string Unit(string metric) => metric.ToLowerInvariant() switch
	{
		"temperature" or "pi_temp" or "pc_temp" => "°C",
		"humidity" or "pi_cpu" or "pi_ram" or "pc_cpu" or "pc_ram" => "%",
		"light" => " lux",
		"co2" => " ppm",
		"tvoc" => " ppb",
		_ => ""
	};

	private static IReadOnlyList<HistoryPoint> Thin(IReadOnlyList<HistoryPoint> points)
	{
		if (points.Count <= MaxSamples) return points;

		int step = (int)Math.Ceiling(points.Count / (double)MaxSamples);
		return points.Where((_, index) => index % step == 0).ToList();
	}
}
