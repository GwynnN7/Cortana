using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class LogEndpoints
{
	public static void MapLogEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Logs}").WithTags("Logs");

		group.MapGet("", Recent)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetLogs")
			.WithSummary("Recent activity, newest first.")
			.Produces<LogListResponse>();

		group.MapDelete("", Clear)
			.Access(EApiAccess.Sensitive)
			.WithName("ClearLogs")
			.WithSummary("Empties the log buffer.")
			.Produces<MessageResponse>();
	}

	private static IResult Recent(HttpRequest request, int limit = 200)
	{
		IReadOnlyList<LogEntry> entries = Notifier.Recent(limit);
		string text = entries.Count == 0
			? "No entries"
			: string.Join("\n", entries.Select(e => $"{e.Timestamp:HH:mm:ss} [{e.Level}] {e.Source}: {e.Message}"));

		return ApiResults.Ok(request, text, new LogListResponse(entries));
	}

	private static IResult Clear(HttpRequest request)
	{
		Notifier.Clear();
		return ApiResults.Message(request, "Logs cleared");
	}
}
