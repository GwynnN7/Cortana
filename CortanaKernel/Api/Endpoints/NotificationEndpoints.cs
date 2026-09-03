using CortanaLib.Primitives;
using CortanaKernel.Application;
using CortanaKernel.Infrastructure.Push;
using CortanaLib.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace CortanaKernel.Api.Endpoints;

public static class NotificationEndpoints
{
	public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/notifications").WithTags("Notifications");

		group.MapGet("", (int? limit, NotificationService notifications, HttpRequest request) =>
			{
				IReadOnlyList<NotificationEntry> entries = notifications.Recent(limit ?? 200);
				string text = entries.Count == 0
					? "Nothing yet"
					: string.Join("\n", entries.Select(entry => $"{entry.Timestamp:HH:mm:ss} [{entry.Level}] {entry.Source}: {entry.Message}"));

				return ApiResults.Ok(request, text, new NotificationListResponse(entries));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Recent activity, newest first").Produces<NotificationListResponse>();

		group.MapDelete("", (NotificationService notifications, HttpRequest request) =>
			{
				notifications.Clear();
				return ApiResults.Message(request, "Cleared");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Empties the activity log");

		group.MapPost("", (NotifyRequest body, NotificationService notifications, HttpRequest request) =>
				ApiResults.From(request, notifications.Send(body)))
			.Access(ApiAccess.Sensitive).WithSummary("Sends a message through one delivery channel, or through all the enabled ones");

		RouteGroupBuilder push = app.MapGroup("/push").WithTags("Push");

		push.MapGet("/key", (PushService service, HttpRequest request) =>
				ApiResults.Ok(request, service.PublicKey, new PushKeyResponse(service.PublicKey)))
			.Access(ApiAccess.ReadOnly).WithSummary("Public VAPID key a browser needs to subscribe").Produces<PushKeyResponse>();

		push.MapGet("", async (PushService service, HttpRequest request) =>
			{
				string line = await service.StatusLine();
				return ApiResults.Ok(request, $"{service.DeviceCount} browser(s) subscribed\nStatus line: {line}",
					new PushDevicesResponse(service.DeviceCount, line));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("How many browsers are subscribed and what the status line says")
			.Produces<PushDevicesResponse>();

		push.MapPost("", (PushDeviceRequest body, PushService service, HttpRequest request) =>
				ApiResults.From(request, service.Subscribe(body)))
			.Access(ApiAccess.Sensitive).WithSummary("Registers a browser for the status notification");

		push.MapDelete("", ([FromBody] PushDeviceRequest body, PushService service, HttpRequest request) =>
				ApiResults.From(request, service.Unsubscribe(body.Endpoint)))
			.Access(ApiAccess.Sensitive).WithSummary("Removes a registered browser");

		push.MapPost("/test", async (PushService service, HttpRequest request) =>
			{
				await service.RefreshStatus(force: true);
				await service.ShowMessage(new NotificationEntry(DateTimeOffset.Now, NotificationSource.Cortana,
					NotificationLevel.Info, "Reading you loud and clear.", "you asked for a test"));

				return ApiResults.Message(request, $"Sent to {service.DeviceCount} browser(s)");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Pushes the status line and a test message to every registered browser");
	}
}
