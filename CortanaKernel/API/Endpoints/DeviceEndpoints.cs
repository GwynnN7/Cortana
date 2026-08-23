using CortanaKernel.Hardware;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class DeviceEndpoints
{
		private const string RoomAlias = "room";

	public static void MapDeviceEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Devices}").WithTags("Devices");

		group.MapGet("", AllDevices)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetDevices")
			.WithSummary("Power state of every device.")
			.Produces<DeviceListResponse>();

		group.MapGet("/{device}", DeviceStatus)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetDevice")
			.WithSummary("Power state of one device.")
			.Produces<DeviceResponse>();

		group.MapPost("/{device}", SwitchDevice)
			.Access(EApiAccess.Sensitive)
			.WithName("SwitchDevice")
			.WithSummary("Switches a device on, off or toggles it. Accepts 'room' to switch the whole room.")
			.Produces<DeviceResponse>();

		group.MapPost("/sleep", Sleep)
			.Access(EApiAccess.Sensitive)
			.WithName("EnterSleepMode")
			.WithSummary("Turns the lamp off and holds manual mode until morning.")
			.Produces<MessageResponse>();
	}

	private static IResult AllDevices(HttpRequest request)
	{
		IReadOnlyList<DeviceResponse> devices = HardwareApi.Devices.GetAllPower();
		string text = string.Join("\n", devices.Select(d => $"{d.Device} is {d.Status}"));
		return ApiResults.Ok(request, text, new DeviceListResponse(devices));
	}

	private static IResult DeviceStatus(string device, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(device, out EDevice parsed)) return ApiResults.UnknownValue<EDevice>(request, "Device", device);

		EStatus status = HardwareApi.Devices.GetPower(parsed);
		return ApiResults.Ok(request, $"{parsed} is {status}", new DeviceResponse(parsed.ToString(), status.ToString()));
	}

	private static IResult SwitchDevice(string device, PostAction? status, HttpRequest request)
	{
		string requested = string.IsNullOrWhiteSpace(status?.Action) ? nameof(ESwitchAction.Toggle) : status!.Action;
		if (!ApiResults.TryParseEnum(requested, out ESwitchAction action)) return ApiResults.UnknownValue<ESwitchAction>(request, "Action", requested);

		string deviceName;
		StringResult result;

		if (ApiResults.TryParseEnum(device, out EDevice parsed))
		{
			deviceName = parsed.ToString();
			result = HardwareApi.Devices.Switch(parsed, action);
		}
		else if (device.Equals(RoomAlias, StringComparison.OrdinalIgnoreCase))
		{
			deviceName = "Room";
			result = HardwareApi.Devices.SwitchRoom(action);
		}
		else
		{
			return ApiResults.UnknownValue<EDevice>(request, "Device", device);
		}

		return ApiResults.From(request, result, value => ($"{deviceName} switched {value}", new DeviceResponse(deviceName, value)));
	}

	private static IResult Sleep(HttpRequest request)
	{
		HardwareApi.Devices.EnterSleepMode();
		return ApiResults.Message(request, "Entering sleep mode");
	}
}
