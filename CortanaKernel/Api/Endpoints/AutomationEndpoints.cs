using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class AutomationEndpoints
{
	public static void MapAutomationEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/automation").WithTags("Automation");

		group.MapGet("", (AutomationService automation, HttpRequest request) =>
			{
				AutomationView view = automation.View();
				return ApiResults.Ok(request, Describe(view), view);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Automation authority, time context, sleep mode and motion")
			.Produces<AutomationView>();

		group.MapPost("", (SwitchRequest? body, AutomationService automation, HttpRequest request) =>
				ApiResults.From(request, automation.SetAutomation(body?.Action ?? SwitchAction.Toggle, RequestOrigin.From(request))))
			.Access(ApiAccess.Sensitive).WithSummary("Turns autonomous automation on, off or toggles it");

		group.MapPost("/sleep", (SwitchRequest? body, AutomationService automation, HttpRequest request) =>
				ApiResults.From(request, automation.SetSleepMode(body?.Action ?? SwitchAction.Toggle, RequestOrigin.From(request))))
			.Access(ApiAccess.Sensitive).WithSummary("Turns sleep mode on, off or toggles it");

		group.MapDelete("/holds", (AutomationService automation, HttpRequest request) =>
				ApiResults.From(request, automation.ReleaseHolds(RequestOrigin.From(request))))
			.Access(ApiAccess.Sensitive)
			.WithSummary("Releases every manual hold and hands control straight back to automation");

		group.MapGet("/diagnostics", (SnapshotService snapshots, NotificationService notifications, HttpRequest request) =>
			{
				AutomationDiagnostics diagnostics = snapshots.Diagnostics(notifications.Recent(15));
				return ApiResults.Ok(request, Diagnose(diagnostics), diagnostics);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Why automation did or did not act just now")
			.Produces<AutomationDiagnostics>();
	}

	private static string Describe(AutomationView view) =>
		string.Join("\n",
			$"Automation: {view.Status}{(view.HoldingUntil is { } until ? $" until {until:HH:mm}" : "")}",
			$"Time context: {view.TimeContext}",
			$"Sleep mode: {(view.SleepMode ? "active" : "inactive")}",
			$"Sleep hold: {(view.SleepHold ? "active" : "inactive")}",
			$"Motion: {(view.MotionActive ? "detected" : "none")}",
			$"Sources: {(view.SourcesOnline ? "all online" : "one or more offline")}");

	private static string Diagnose(AutomationDiagnostics diagnostics) =>
		string.Join("\n",
			Describe(diagnostics.Automation),
			$"Last decision: {diagnostics.LastDecision}",
			"",
			"Decisions:",
			string.Join("\n", diagnostics.RecentDecisions.Take(10)
				.Select(record => $"  {record.At:HH:mm:ss} {record.Subject} -> {record.Outcome} ({record.Reason})")));
}
