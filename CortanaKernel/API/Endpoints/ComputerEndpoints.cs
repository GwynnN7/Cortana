using CortanaKernel.Hardware;
using CortanaKernel.Kernel;
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

		group.MapGet("/metrics", Metrics)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetComputerMetrics")
			.WithSummary("Latest performance and temperature snapshot from the desktop agent.")
			.Produces<MetricsResponse>();

		group.MapPost("/metrics", PushMetrics)
			.Access(EApiAccess.Sensitive)
			.WithName("PushComputerMetrics")
			.WithSummary("Stores a performance snapshot pushed by the desktop agent.")
			.Produces<MessageResponse>();
	}

	private static IResult Metrics(HttpRequest request) =>
		MetricsStore.Latest().Match<IResult>(
			metrics => ApiResults.Ok(request, MetricsStore.Render(metrics), metrics),
			() => ApiResults.NotFound(request, "No metrics received from the desktop agent yet"));

	private static IResult PushMetrics(PostMetrics metrics, HttpRequest request)
	{
		MetricsStore.Store(metrics);
		return ApiResults.Message(request, "Metrics stored");
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
