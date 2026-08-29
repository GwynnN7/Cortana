using Microsoft.AspNetCore.Mvc;
using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class PushEndpoints
{
	public static void MapPushEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Push}").WithTags("Push");

		group.MapGet("/key", Key)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetPushKey")
			.WithSummary("Public VAPID key a browser needs to subscribe.")
			.Produces<PushKeyResponse>();

		group.MapGet("", Devices)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetPushDevices")
			.WithSummary("How many devices are subscribed.")
			.Produces<PushDeviceListResponse>();

		group.MapPost("", Subscribe)
			.Access(EApiAccess.Sensitive)
			.WithName("SubscribePush")
			.WithSummary("Registers a browser for push notifications.")
			.Produces<MessageResponse>();

		group.MapDelete("", Unsubscribe)
			.Access(EApiAccess.Sensitive)
			.WithName("UnsubscribePush")
			.WithSummary("Removes a registered browser.")
			.Produces<MessageResponse>();

		group.MapPost("/test", Test)
			.Access(EApiAccess.Sensitive)
			.WithName("TestPush")
			.WithSummary("Sends a test notification to every registered device.")
			.Produces<MessageResponse>();
	}

	private static IResult Key(HttpRequest request) =>
		ApiResults.Ok(request, PushService.PublicKey, new PushKeyResponse(PushService.PublicKey));

	private static IResult Devices(HttpRequest request) =>
		ApiResults.Ok(request, $"{PushService.DeviceCount} device(s) subscribed\nStatus line: {PushService.StatusLine()}",
			new PushDeviceListResponse(PushService.DeviceCount));

	private static IResult Subscribe(PostPushDevice device, HttpRequest request) =>
		ApiResults.From(request, PushService.Subscribe(device));

	private static IResult Unsubscribe([FromBody] PostPushDevice device, HttpRequest request) =>
		ApiResults.From(request, PushService.Unsubscribe(device.Endpoint));

	private static async Task<IResult> Test(HttpRequest request)
	{
		await PushService.Broadcast("Hi, I'm Cortana");
		return ApiResults.Message(request, $"Test sent to {PushService.DeviceCount} device(s)");
	}
}
