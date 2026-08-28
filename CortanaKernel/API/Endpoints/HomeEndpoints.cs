using System.Text.Json;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class HomeEndpoints
{
	private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan CoalesceWindow = TimeSpan.FromMilliseconds(120);

	public static void MapHomeEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("/", Identity)
			.Access(EApiAccess.Public)
			.WithName("GetIdentity")
			.WithSummary("Identifies the service. Reachable without an API key.")
			.WithTags("Home");

		app.MapGet("/health", Health)
			.Access(EApiAccess.Public)
			.WithName("GetHealth")
			.WithSummary("Liveness probe. Reachable without an API key.")
			.WithTags("Home");

		app.MapGet("/events", Events)
			.Access(EApiAccess.ReadOnly)
			.WithName("StreamSystemEvents")
			.WithSummary("Server-sent stream of system snapshots, pushed on change.")
			.WithTags("Home");

		app.MapGet("/status", Status)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSystemStatus")
			.WithSummary("Full system snapshot in one request.")
			.WithTags("Home")
			.Produces<SystemStatusResponse>();
	}

	private static IResult Identity(HttpRequest request) => ApiResults.Message(request, "Hi, I'm Cortana");

	private static IResult Health(HttpRequest request) => ApiResults.Message(request, "OK");

		private static async Task<IResult> Status(HttpRequest request) =>
		ApiResults.Ok(request, "Cortana is online", await Snapshot());

		private static async Task Events(HttpContext context, CancellationToken token)
	{
		context.Response.Headers.ContentType = "text/event-stream";
		context.Response.Headers.CacheControl = "no-cache";
		
		context.Response.Headers["X-Accel-Buffering"] = "no";

		using SystemEvents.Subscription subscription = SystemEvents.Subscribe();
		await Send(context, await Snapshot(), token);

		while (!token.IsCancellationRequested)
		{
			try
			{
				using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token);
				heartbeat.CancelAfter(HeartbeatInterval);
				await subscription.Reader.ReadAsync(heartbeat.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested)
			{
			}
			catch (OperationCanceledException)
			{
				return;
			}

			await Task.Delay(CoalesceWindow, token);
			await Send(context, await Snapshot(), token);
		}
	}

	private static async Task Send(HttpContext context, SystemStatusResponse snapshot, CancellationToken token)
	{
		string json = JsonSerializer.Serialize(snapshot, DataHandler.ApiSerializerOptions);
		await context.Response.WriteAsync($"event: status\ndata: {json}\n\n", token);
		await context.Response.Body.FlushAsync(token);
	}

	private static async Task<SystemStatusResponse> Snapshot() => new(
		Devices: HardwareApi.Devices.GetAllPower(),
		Sensors: HardwareApi.Sensors.GetAllData(),
		Settings: HardwareApi.Sensors.GetAllSettings(),
		Raspberry: await HardwareApi.Raspberry.GetAllHardwareInfo(),
		Subfunctions: await Bootloader.GetAllStatuses(),
		Computer: MetricsStore.Latest().Match<MetricsResponse?>(metrics => metrics, () => null),
		Automation: new AutomationResponse(AutomationService.State.ToString(), AutomationService.ManualMinutesLeft),
		Timestamp: DateTimeOffset.Now);
}
