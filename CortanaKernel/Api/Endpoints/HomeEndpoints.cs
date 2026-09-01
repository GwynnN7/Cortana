using System.Text.Json;
using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Api.Endpoints;

public static class HomeEndpoints
{
	private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(20);
	private static readonly TimeSpan Coalesce = TimeSpan.FromMilliseconds(120);

	public static void MapHomeEndpoints(this IEndpointRouteBuilder app)
	{
		app.MapGet("/", (HttpRequest request) => ApiResults.Message(request, "Hi, I'm Cortana"))
			.Access(ApiAccess.Public).WithTags("Home").WithSummary("Identifies the service. Reachable without an API key");

		app.MapGet("/health", (HttpRequest request) => ApiResults.Message(request, "OK"))
			.Access(ApiAccess.Public).WithTags("Home").WithSummary("Liveness probe. Reachable without an API ke.");

		app.MapGet("/snapshot", async (SnapshotService snapshots, HttpRequest request, CancellationToken token) =>
				ApiResults.Ok(request, "Cortana is online", await snapshots.Build(token)))
			.Access(ApiAccess.ReadOnly).WithTags("Home").WithSummary("The whole system state in one read")
			.Produces<CortanaSnapshot>();

		app.MapGet("/events", StreamState)
			.Access(ApiAccess.ReadOnly).WithTags("Home").WithSummary("Server-sent stream of snapshots, pushed whenever anything changes");

		app.MapGet("/events/notifications", StreamNotifications)
			.Access(ApiAccess.ReadOnly).WithTags("Home").WithSummary("Server-sent stream of notifications for one delivery channel");
	}

	/// The client fetches a snapshot, then receives a newer one on every change
	private static async Task StreamState(HttpContext context, SnapshotService snapshots, StateBroadcaster broadcaster, CancellationToken token)
	{
		Prepare(context);

		using StateBroadcaster.StateSubscription subscription = broadcaster.SubscribeState();
		await Send(context, "status", await snapshots.Build(token), token);

		while (!token.IsCancellationRequested)
		{
			try
			{
				using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(token);
				heartbeat.CancelAfter(Heartbeat);
				await subscription.Reader.ReadAsync(heartbeat.Token);
			}
			catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
			catch (OperationCanceledException)
			{
				return;
			}

			await Task.Delay(Coalesce, token);
			await Send(context, "status", await snapshots.Build(token), token);
		}
	}

	private static async Task StreamNotifications(HttpContext context, StateBroadcaster broadcaster, string? channel, CancellationToken token)
	{
		if (!Enum.TryParse(channel, true, out NotificationChannel target)) target = NotificationChannel.Web;

		Prepare(context);

		using StateBroadcaster.NotificationSubscription subscription = broadcaster.SubscribeNotifications(target);

		while (!token.IsCancellationRequested)
		{
			NotificationEnvelope envelope;
			try
			{
				envelope = await subscription.Reader.ReadAsync(token);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			await Send(context, "notification", envelope, token);
		}
	}

	private static void Prepare(HttpContext context)
	{
		context.Response.Headers.ContentType = "text/event-stream";
		context.Response.Headers.CacheControl = "no-cache";
		context.Response.Headers["X-Accel-Buffering"] = "no";
	}

	private static async Task Send<T>(HttpContext context, string name, T payload, CancellationToken token)
	{
		string json = JsonSerializer.Serialize(payload, CortanaEnvironment.WireJson);
		await context.Response.WriteAsync($"event: {name}\ndata: {json}\n\n", token);
		await context.Response.Body.FlushAsync(token);
	}
}
