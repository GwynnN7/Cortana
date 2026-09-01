using CortanaKernel.Application;
using CortanaLib.Contracts;

namespace CortanaKernel.Api.Endpoints;

public static class HistoryEndpoints
{
	public static void MapHistoryEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/history").WithTags("History");

		group.MapGet("", (HistoryService history, HttpRequest request) =>
			{
				HistoryInfoResponse info = history.Info();
				string text = $"Recording {string.Join(", ", info.Metrics)}\n" +
					$"Every {info.SampleMinutes} min, kept {info.RetentionDays} days, using {info.Bytes / 1024} KB";

				return ApiResults.Ok(request, text, info);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Which metrics are recorded, how often and for how long.")
			.Produces<HistoryInfoResponse>();

		group.MapGet("/{metric}", (string metric, int? hours, DateTimeOffset? until, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.Series(metric, hours ?? 24, until), series =>
					$"{series.Metric} over {hours ?? 24}h: minimum {series.Min}{series.Unit}, maximum {series.Max}{series.Unit}, " +
					$"average {series.Average}{series.Unit} ({series.Points} samples)"))
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Recorded values for one metric. 'until' pages the window back through time.")
			.Produces<HistorySeries>();

		group.MapPost("/analysis", (AnalysisRequest body, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.Analyse(body), result => result.Summary))
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Runs a deterministic calculation over recorded data: averages, extremes, trends, durations, worst periods and comparisons.")
			.Produces<AnalysisResult>();
	}
}
