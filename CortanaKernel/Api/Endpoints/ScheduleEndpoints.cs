using CortanaKernel.Application;
using CortanaLib.Contracts;

namespace CortanaKernel.Api.Endpoints;

public static class ScheduleEndpoints
{
	public static void MapScheduleEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/schedules").WithTags("Schedules");

		group.MapGet("", (ScheduleService schedules, HttpRequest request) =>
			{
				IReadOnlyList<ScheduleView> views = schedules.Views();
				string text = views.Count == 0 ? "No schedules" : string.Join("\n", views.Select(Describe));
				return ApiResults.Ok(request, text, new ScheduleListResponse(views));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every schedule with its next run time").Produces<ScheduleListResponse>();

		group.MapGet("/{id}", (string id, ScheduleService schedules, HttpRequest request) =>
			{
				Schedule? schedule = schedules.Get(id);
				if (schedule == null) return ApiResults.NotFound(request, $"Schedule '{id}' not found");

				var view = new ScheduleView(schedule, CortanaKernel.Domain.Scheduling.ScheduleTiming.NextRun(schedule, DateTimeOffset.Now));
				return ApiResults.Ok(request, Describe(view), view);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One schedule").Produces<ScheduleView>();

		group.MapPost("", (CreateScheduleRequest body, ScheduleService schedules, HttpRequest request) =>
				schedules.Create(body).Match(
					schedule =>
					{
						var view = new ScheduleView(schedule, CortanaKernel.Domain.Scheduling.ScheduleTiming.NextRun(schedule, DateTimeOffset.Now));
						return ApiResults.Ok(request, Describe(view), view);
					},
					error => ApiResults.BadRequest(request, error)))
			.Access(ApiAccess.Sensitive).WithSummary("Creates a schedule that runs an action at a time, on an interval, or when something happens")
			.Produces<ScheduleView>();

		group.MapPost("/{id}", async (string id, ScheduleCommandRequest body, ScheduleService schedules, HttpRequest request) =>
			{
				switch (body.Command?.ToLowerInvariant())
				{
					case "enable":
					case "disable":
						Schedule? updated = schedules.SetEnabled(id, body.Command.Equals("enable", StringComparison.OrdinalIgnoreCase));
						if (updated == null) return ApiResults.NotFound(request, $"Schedule '{id}' not found");

						var view = new ScheduleView(updated, CortanaKernel.Domain.Scheduling.ScheduleTiming.NextRun(updated, DateTimeOffset.Now));
						return ApiResults.Ok(request, Describe(view), view);

					case "run":
						return ApiResults.From(request, await schedules.RunNow(id));

					default:
						return ApiResults.BadRequest(request, "The command must be enable, disable or run");
				}
			})
			.Access(ApiAccess.Sensitive).WithSummary("Runs a schedule now, or turns it on and off");

		group.MapDelete("/{id}", (string id, ScheduleService schedules, HttpRequest request) =>
				schedules.Delete(id)
					? ApiResults.Message(request, $"Schedule '{id}' deleted")
					: ApiResults.NotFound(request, $"Schedule '{id}' not found"))
			.Access(ApiAccess.Sensitive).WithSummary("Removes a schedule");
	}

	private static string Describe(ScheduleView view)
	{
		Schedule schedule = view.Schedule;
		string when = schedule.Trigger switch
		{
			ScheduleTrigger.Once => $"once at {schedule.At:dd MMM HH:mm}",
			ScheduleTrigger.Interval => $"every {schedule.IntervalSeconds}s",
			ScheduleTrigger.Daily => $"daily at {schedule.Hour:00}:{schedule.Minute:00}",
			ScheduleTrigger.Weekly => $"{schedule.Day} at {schedule.Hour:00}:{schedule.Minute:00}",
			ScheduleTrigger.Event => $"on {schedule.Event}",
			_ => "unknown"
		};

		string next = view.NextRun == null ? "" : $" -> {view.NextRun:dd MMM HH:mm}";
		string state = schedule.Enabled ? "" : " [disabled]";
		return $"[{schedule.Id}] {schedule.Name}: {when}{next}{state}";
	}
}
