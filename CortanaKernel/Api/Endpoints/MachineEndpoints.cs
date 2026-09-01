using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Api.Endpoints;

/// The desktop computer, the Raspberry and their metrics.
public static class MachineEndpoints
{
	public static void MapMachineEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder computer = app.MapGroup("/computer").WithTags("Computer");

		computer.MapGet("", (DeviceService devices, HttpRequest request) =>
			{
				bool connected = devices.ComputerConnected;
				return ApiResults.Ok(request, connected ? "The computer is on" : "The computer is off",
					new DeviceView(DeviceId.Computer, connected ? PowerState.On : PowerState.Off, null));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Whether the desktop agent is connected.").Produces<DeviceView>();

		computer.MapPost("", async (ComputerRequest body, DeviceService devices, HttpRequest request, CancellationToken token) =>
				ApiResults.From(request, await devices.CommandComputer(body.Command, body.Argument, RequestOrigin.From(request), token)))
			.Access(ApiAccess.Sensitive).WithSummary("Sends a command to the desktop agent.");

		RouteGroupBuilder raspberry = app.MapGroup("/raspberry").WithTags("Raspberry");

		raspberry.MapGet("", async (DeviceService devices, HttpRequest request, CancellationToken token) =>
			{
				IReadOnlyList<RaspberryInfoView> info = await devices.HostInformation(token);
				return ApiResults.Ok(request, string.Join("\n", info.Select(view => $"{view.Info}: {view.Value}{view.Unit}")), info);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Temperature, location, gateway and public IP.")
			.Produces<IReadOnlyList<RaspberryInfoView>>();

		raspberry.MapGet("/{info}", async (string info, DeviceService devices, HttpRequest request, CancellationToken token) =>
			{
				if (!ApiResults.TryParse(info, out RaspberryInfo parsed)) return ApiResults.Unknown<RaspberryInfo>(request, "Property", info);

				return ApiResults.From(request, await devices.HostInformation(parsed, token),
					value => (value + Units.For(parsed), new RaspberryInfoView(parsed, value, Units.For(parsed))));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One Raspberry Pi property.").Produces<RaspberryInfoView>();

		raspberry.MapPost("", async (RaspberryRequest body, DeviceService devices, HttpRequest request, CancellationToken token) =>
				ApiResults.From(request, await devices.CommandRaspberry(body.Command, body.Argument, token)))
			.Access(ApiAccess.Sensitive).WithSummary("Shuts down, reboots, or runs a shell command on the Pi.");

		RouteGroupBuilder metrics = app.MapGroup("/metrics").WithTags("Metrics");

		metrics.MapGet("/computer", (MetricsService service, HttpRequest request) =>
				service.Computer() is { } view
					? ApiResults.Ok(request, MachineMetrics.Render(view), view)
					: ApiResults.NotFound(request, "The computer has not reported any metrics yet"))
			.Access(ApiAccess.ReadOnly).WithSummary("Latest sample pushed by the desktop agent.").Produces<MetricsView>();

		metrics.MapPost("/computer", (MachineSample sample, MetricsService service, HttpRequest request) =>
			{
				service.StoreComputer(sample);
				return ApiResults.Message(request, "Metrics stored");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Stores a sample pushed by the desktop agent.");

		metrics.MapGet("/raspberry", (MetricsService service, HttpRequest request) =>
			{
				MetricsView view = service.Raspberry();
				return ApiResults.Ok(request, MachineMetrics.Render(view), view);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("CPU, RAM, disk and temperature of the Pi.").Produces<MetricsView>();
	}
}
