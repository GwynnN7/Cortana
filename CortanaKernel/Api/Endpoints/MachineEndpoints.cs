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
					devices.Machine ?? new DeviceView(DeviceIds.Computer, "Computer", connected ? "🖥" : "💤",
						SourceIds.Computer, connected ? PowerState.On : PowerState.Off, null));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Whether the desktop agent is connected").Produces<DeviceView>();

		computer.MapPost("", async (ComputerRequest body, DeviceService devices, HttpRequest request, CancellationToken token) =>
				ApiResults.From(request, await devices.CommandComputer(body.Command, body.Argument, RequestOrigin.From(request), token)))
			.Access(ApiAccess.Sensitive).WithSummary("Sends a command to the desktop agent");

		RouteGroupBuilder raspberry = app.MapGroup("/raspberry").WithTags("Raspberry");

		raspberry.MapGet("", async (DeviceService devices, HttpRequest request, CancellationToken token) =>
			{
				IReadOnlyList<RaspberryInfoView> info = await devices.HostInformation(token);
				return ApiResults.Ok(request, string.Join("\n", info.Select(view => $"{view.Info}: {view.Value}{view.Unit}")), info);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Temperature, location, gateway and public IP")
			.Produces<IReadOnlyList<RaspberryInfoView>>();

		raspberry.MapGet("/{info}", async (string info, DeviceService devices, HttpRequest request, CancellationToken token) =>
			{
				if (!ApiResults.TryParse(info, out RaspberryInfo parsed)) return ApiResults.Unknown<RaspberryInfo>(request, "Property", info);

				return ApiResults.From(request, await devices.HostInformation(parsed, token),
					value => (value + Units.For(parsed), new RaspberryInfoView(parsed, value, Units.For(parsed))));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One Raspberry Pi property").Produces<RaspberryInfoView>();

		raspberry.MapPost("", async (RaspberryRequest body, DeviceService devices, HttpRequest request, CancellationToken token) =>
				ApiResults.From(request, await devices.CommandRaspberry(body.Command, body.Argument, token)))
			.Access(ApiAccess.Sensitive).WithSummary("Shuts down, reboots, or runs a shell command on the Pi");

		RouteGroupBuilder metrics = app.MapGroup("/metrics").WithTags("Metrics");

		metrics.MapGet("/computer", (SensorService sensors, HttpRequest request) =>
				sensors.Source(SourceIds.Computer) is { } view
					? ApiResults.Ok(request, Describe(view, sensors), view)
					: ApiResults.NotFound(request, "The computer has not reported anything yet"))
			.Access(ApiAccess.ReadOnly).WithSummary("What the desktop says about itself").Produces<SourceView>();

		metrics.MapGet("/raspberry", (SensorService sensors, HttpRequest request) =>
				sensors.Source(SourceIds.Raspberry) is { } view
					? ApiResults.Ok(request, Describe(view, sensors), view)
					: ApiResults.NotFound(request, "The Pi has not reported anything yet"))
			.Access(ApiAccess.ReadOnly).WithSummary("What the Pi says about itself").Produces<SourceView>();
	}

	/// A source in plain text: what it says about itself, then what it is reading
	private static string Describe(SourceView view, SensorService sensors)
	{
		var lines = new List<string> { $"{view.Id} ({view.State.ToString().ToLowerInvariant()})" };

		lines.AddRange(view.Facts.Select(fact => $"{fact.Key}: {fact.Value}"));
		lines.AddRange(sensors.All()
			.Where(sensor => sensor.Source == view.Id && sensor.Available)
			.Select(sensor => $"{sensor.Name}: {sensor.Value}{sensor.Unit}"));

		return string.Join("\n", lines);
	}
}
