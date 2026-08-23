using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class ScheduleEndpoints
{
	public static void MapScheduleEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Schedules}").WithTags("Schedules");

		group.MapGet("", All)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSchedules")
			.WithSummary("Every schedule with its next run time.")
			.Produces<ScheduleListResponse>();

		group.MapGet("/{id}", One)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSchedule")
			.WithSummary("One schedule.")
			.Produces<ScheduleResponse>();

		group.MapPost("", Create)
			.Access(EApiAccess.Sensitive)
			.WithName("CreateSchedule")
			.WithSummary("Creates a schedule. Triggers: Once, Interval, Daily, Weekly, Event.")
			.Produces<ScheduleResponse>();

		group.MapPost("/{id}", Update)
			.Access(EApiAccess.Sensitive)
			.WithName("UpdateSchedule")
			.WithSummary("Commands: enable, disable, run.")
			.Produces<ScheduleResponse>();

		group.MapDelete("/{id}", Delete)
			.Access(EApiAccess.Sensitive)
			.WithName("DeleteSchedule")
			.WithSummary("Removes a schedule.")
			.Produces<MessageResponse>();
	}

	private static IResult All(HttpRequest request)
	{
		List<ScheduleResponse> schedules = ScheduleService.All()
			.Select(s => new ScheduleResponse(s, ScheduleService.NextRun(s)))
			.ToList();

		string text = schedules.Count == 0
			? "No schedules"
			: string.Join("\n", schedules.Select(Describe));

		return ApiResults.Ok(request, text, new ScheduleListResponse(schedules));
	}

	private static IResult One(string id, HttpRequest request)
	{
		Schedule? schedule = ScheduleService.Get(id);
		if (schedule == null) return ApiResults.NotFound(request, $"Schedule '{id}' not found");

		var response = new ScheduleResponse(schedule, ScheduleService.NextRun(schedule));
		return ApiResults.Ok(request, Describe(response), response);
	}

	private static IResult Create(PostSchedule body, HttpRequest request)
	{
		return ScheduleService.Create(body).Match(
			schedule =>
			{
				var response = new ScheduleResponse(schedule, ScheduleService.NextRun(schedule));
				return ApiResults.Ok(request, Describe(response), response);
			},
			error => ApiResults.BadRequest(request, error));
	}

	private static async Task<IResult> Update(string id, PostScheduleUpdate body, HttpRequest request)
	{
		switch (body.Command?.ToLowerInvariant())
		{
			case "enable":
			case "disable":
				Schedule? updated = ScheduleService.SetEnabled(id, body.Command.Equals("enable", StringComparison.OrdinalIgnoreCase));
				if (updated == null) return ApiResults.NotFound(request, $"Schedule '{id}' not found");

				var response = new ScheduleResponse(updated, ScheduleService.NextRun(updated));
				return ApiResults.Ok(request, Describe(response), response);

			case "run":
				return ApiResults.From(request, await ScheduleService.RunNow(id));

			default:
				return ApiResults.BadRequest(request, "Command must be enable, disable or run");
		}
	}

	private static IResult Delete(string id, HttpRequest request) =>
		ScheduleService.Delete(id)
			? ApiResults.Message(request, $"Schedule '{id}' deleted")
			: ApiResults.NotFound(request, $"Schedule '{id}' not found");

	private static string Describe(ScheduleResponse response)
	{
		Schedule s = response.Schedule;
		string when = s.Trigger switch
		{
			EScheduleTrigger.Once => $"once at {s.At:dd MMM HH:mm}",
			EScheduleTrigger.Interval => $"every {s.IntervalSeconds}s",
			EScheduleTrigger.Daily => $"daily at {s.Hour:00}:{s.Minute:00}",
			EScheduleTrigger.Weekly => $"{s.Day} at {s.Hour:00}:{s.Minute:00}",
			EScheduleTrigger.Event => $"on {s.Event}",
			_ => "unknown"
		};

		string next = response.NextRun == null ? "" : $" -> {response.NextRun:dd MMM HH:mm}";
		string state = s.Enabled ? "" : " [disabled]";
		return $"[{s.Id}] {s.Name}: {when}{next}{state}";
	}
}
