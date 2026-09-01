using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class ServiceEndpoints
{
	public static void MapServiceEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/services").WithTags("Services");

		group.MapGet("", async (ServiceControlService services, HttpRequest request, CancellationToken token) =>
			{
				IReadOnlyList<ServiceView> all = await services.All(token);
				string text = string.Join("\n", all.Select(view => $"{view.Service} is {(view.Running ? "running" : "not running")}"));
				return ApiResults.Ok(request, text, all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Running state of every Cortana service.").Produces<IReadOnlyList<ServiceView>>();

		group.MapGet("/{service}", async (string service, ServiceControlService services, HttpRequest request, CancellationToken token) =>
			{
				if (!ApiResults.TryParse(service, out ServiceId parsed)) return ApiResults.Unknown<ServiceId>(request, "Service", service);

				bool running = await services.IsRunning(parsed, token);
				return ApiResults.Ok(request, running ? $"{parsed} is running" : $"{parsed} is not running", new ServiceView(parsed, running));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Running state of one service.").Produces<ServiceView>();

		group.MapPost("/{service}", async (string service, ServiceRequest body, ServiceControlService services, HttpRequest request, CancellationToken token) =>
			{
				if (!ApiResults.TryParse(service, out ServiceId parsed)) return ApiResults.Unknown<ServiceId>(request, "Service", service);

				return ApiResults.From(request, await services.Control(parsed, body.Action, token));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Starts, stops, restarts or updates a service.");

		group.MapGet("/{service}/journal", async (string service, int? lines, ServiceControlService services, HttpRequest request, CancellationToken token) =>
			{
				if (!ApiResults.TryParse(service, out ServiceId parsed)) return ApiResults.Unknown<ServiceId>(request, "Service", service);

				return ApiResults.From(request, await services.Journal(parsed, lines ?? 100, token));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Recent systemd journal lines for one service.");
	}
}
