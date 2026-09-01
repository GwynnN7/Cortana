using CortanaKernel.Application;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class DeviceEndpoints
{
	private const string RoomAlias = "room";

	public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/devices").WithTags("Devices");

		group.MapGet("", (DeviceService devices, HttpRequest request) =>
			{
				IReadOnlyList<DeviceView> all = devices.All();
				return ApiResults.Ok(request, string.Join("\n", all.Select(view => $"{view.Device} is {view.State}")), all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Power state of every device.").Produces<IReadOnlyList<DeviceView>>();

		group.MapGet("/{device}", (string device, DeviceService devices, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(device, out DeviceId parsed)) return ApiResults.Unknown<DeviceId>(request, "Device", device);

				PowerState state = devices.State(parsed);
				return ApiResults.Ok(request, $"{parsed} is {state}", new DeviceView(parsed, state, null));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Power state of one device.").Produces<DeviceView>();

		group.MapPost("/{device}", (string device, SwitchRequest? body, DeviceService devices, HttpRequest request) =>
			{
				SwitchAction action = body?.Action ?? SwitchAction.Toggle;
				CommandOrigin origin = RequestOrigin.From(request);

				if (device.Equals(RoomAlias, StringComparison.OrdinalIgnoreCase))
					return ApiResults.From(request, devices.SwitchRoom(action, origin));

				if (!ApiResults.TryParse(device, out DeviceId parsed)) return ApiResults.Unknown<DeviceId>(request, "Device", device);

				return ApiResults.From(request, devices.Switch(parsed, action, origin),
					value => ($"{parsed} switched {value}", new DeviceView(parsed, devices.State(parsed), null)));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Switches a device on, off or toggles it. Accepts 'room' for the whole room.")
			.Produces<DeviceView>();
	}
}
