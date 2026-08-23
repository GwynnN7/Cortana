using CortanaKernel.Hardware;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class ComputerEndpoints
{
	public static void MapComputerEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Computer}").WithTags("Computer");

		group.MapGet("", Status)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetComputerStatus")
			.WithSummary("Whether the desktop agent is connected.")
			.Produces<DeviceResponse>();

		group.MapPost("", Command)
			.Access(EApiAccess.Sensitive)
			.WithName("CommandComputer")
			.WithSummary("Sends a command to the desktop agent.")
			.Produces<MessageResponse>();
	}

	private static IResult Status(HttpRequest request)
	{
		EStatus status = HardwareApi.Devices.GetPower(EDevice.Computer);
		return ApiResults.Ok(request, $"Computer is {status}", new DeviceResponse(nameof(EDevice.Computer), status.ToString()));
	}

	private static async Task<IResult> Command(PostCommand command, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(command.Command, out EComputerCommand parsed))
			return ApiResults.UnknownValue<EComputerCommand>(request, "Command", command.Command);

		StringResult result = await HardwareApi.Devices.CommandComputer(parsed, string.IsNullOrEmpty(command.Args) ? null : command.Args);
		return ApiResults.From(request, result);
	}
}
