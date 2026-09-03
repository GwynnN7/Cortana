using CortanaKernel.Application;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class DeviceEndpoints
{
	public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/devices").WithTags("Devices");

		group.MapGet("", (DeviceService devices, HttpRequest request) =>
			{
				IReadOnlyList<DeviceView> all = devices.All();
				return ApiResults.Ok(request, string.Join("\n", all.Select(view => $"{view.Name} is {view.State}")), all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Power state of every device").Produces<IReadOnlyList<DeviceView>>();

		group.MapGet("/{device}", (string device, DeviceService devices, HttpRequest request) =>
			{
				if (devices.Describe(device) is not { } known) return ApiResults.NotFound(request, $"Unknown device '{device}'");

				return ApiResults.Ok(request, $"{known.Name} is {known.State}", known);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Power state of one device").Produces<DeviceView>();

		group.MapPost("/{device}", (string device, SwitchRequest? body, DeviceService devices, HttpRequest request) =>
			{
				SwitchAction action = body?.Action ?? SwitchAction.Toggle;
				CommandOrigin origin = RequestOrigin.From(request);

				if (!devices.Known(device)) return ApiResults.NotFound(request, $"Unknown device '{device}'");

				return ApiResults.From(request, devices.Switch(device, action, origin),
					value => ($"{devices.Describe(device)?.Name ?? device} switched {value}", devices.Describe(device)!));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Switches a device on, off or toggles it")
			.Produces<DeviceView>();
	}
}
