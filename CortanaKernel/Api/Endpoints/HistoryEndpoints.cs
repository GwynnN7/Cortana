using CortanaKernel.Application;
using CortanaKernel.Domain.History;
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
			.Access(ApiAccess.ReadOnly).WithSummary("Which metrics are recorded, how often and for how long")
			.Produces<HistoryInfoResponse>();

		group.MapGet("/days", (int? days, HistoryService history, HttpRequest request) =>
			{
				IReadOnlyList<DaySummary> all = history.Days(days ?? 30);

				string text = all.Count == 0
					? "No day has been summarised yet"
					: string.Join("\n", all.Select(day =>
						$"{day.Day:ddd dd MMM}: up {DayRhythm.Spell(day.FirstPresence)}, " +
						$"pc {DayRhythm.Spell(day.ComputerOn)}-{DayRhythm.Spell(day.ComputerOff)}, " +
						$"{day.ComputerMinutes / 60:0.#}h at it"));

				return ApiResults.Ok(request, text, new DaySummaryListResponse(all));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One row per day: the numbers a rhythm is made of")
			.Produces<DaySummaryListResponse>();

		group.MapPost("/days", (int? days, HistoryService history, HttpRequest request) =>
				ApiResults.Message(request, $"{history.Backfill(days ?? 60)} day(s) summarised from what is on disk"))
			.Access(ApiAccess.Sensitive).WithSummary("Summarises the days already recorded, for a rhythm to learn from");

		group.MapGet("/rhythm/{metric}", (string metric, int? weeks, HistoryService history, HttpRequest request) =>
			{
				RhythmView view = history.Rhythm(metric, weeks ?? 8);
				return ApiResults.Ok(request, view.Summary, view);
			})
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Today against the usual for this weekday: up, bed, computerOn, computerOff or sleep")
			.Produces<RhythmView>();

		group.MapGet("/{metric}", (string metric, int? hours, DateTimeOffset? until, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.Series(metric, hours ?? 24, until), series =>
					$"{series.Metric} over {hours ?? 24}h: minimum {series.Min}{series.Unit}, maximum {series.Max}{series.Unit}, " +
					$"average {series.Average}{series.Unit} ({series.Points} samples)"))
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Recorded values for one metric. 'until' pages the window back through time")
			.Produces<HistorySeries>();

		group.MapGet("/{metric}/usual", (string metric, int? days, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.CompareToUsual(metric, days ?? 21), result => result.Summary))
			.Access(ApiAccess.ReadOnly).WithTags("History").WithSummary("How the latest reading compares with this hour's usual range");

		group.MapGet("/{metric}/against/{other}", (string metric, string other, int? hours, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.Correlate(metric, other, hours ?? 24), result => result.Summary))
			.Access(ApiAccess.ReadOnly).WithSummary("How two recorded metrics move together");

		group.MapGet("/{metric}/during/{category}", (string metric, string category, int? hours, HistoryService history, HttpRequest request) =>
				!Enum.TryParse(category, true, out ActivityCategory parsed)
					? ApiResults.Problem(request, StatusCodes.Status400BadRequest, "Bad request",
						$"Unknown activity '{category}'. Valid: {string.Join(", ", Enum.GetNames<ActivityCategory>())}")
					: ApiResults.From(request, history.DuringActivity(metric, parsed, hours ?? 72), result => result.Summary))
			.Access(ApiAccess.ReadOnly).WithSummary("A room metric while the desktop is doing one thing, against everything else");

		group.MapGet("/{metric}/session", (string metric, int? hours, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.ThisSession(metric, hours ?? 12), result => result.Summary))
			.Access(ApiAccess.ReadOnly).WithSummary("How a room metric has moved since the current desktop session began");

		group.MapPost("/analysis", (AnalysisRequest body, HistoryService history, HttpRequest request) =>
				ApiResults.From(request, history.Analyse(body), result => result.Summary))
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Answers a question about recorded data exactly, so nothing has to be estimated")
			.Produces<AnalysisResult>();
	}
}
