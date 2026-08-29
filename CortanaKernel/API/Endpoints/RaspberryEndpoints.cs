using System.Globalization;
using CortanaKernel.Hardware;
using CortanaKernel.Kernel;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class RaspberryEndpoints
{
	public static void MapRaspberryEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Raspberry}").WithTags("Raspberry");

		group.MapGet("", AllInfo)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetRaspberryInfo")
			.WithSummary("Temperature, location, gateway and public IP.")
			.Produces<RaspberryListResponse>();

		group.MapGet("/metrics", Metrics)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetRaspberryMetrics")
			.WithSummary("CPU, RAM, disk and temperature of the Raspberry itself.")
			.Produces<MetricsResponse>();

		group.MapGet("/{info}", GetInfo)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetRaspberryInfoItem")
			.WithSummary("One Raspberry Pi property.")
			.Produces<SensorResponse>();

		group.MapPost("", Command)
			.Access(EApiAccess.Sensitive)
			.WithName("CommandRaspberry")
			.WithSummary("Shuts down, reboots, or runs a shell command on the Pi.")
			.Produces<MessageResponse>();
	}

	private static IResult Metrics(HttpRequest request)
	{
		MetricsResponse metrics = MetricsStore.Local();
		return ApiResults.Ok(request, MetricsStore.Render(metrics), metrics);
	}

	private static async Task<IResult> AllInfo(HttpRequest request)
	{
		IReadOnlyList<SensorResponse> info = await HardwareApi.Raspberry.GetAllHardwareInfo();
		string text = string.Join("\n", info.Select(i => $"{i.Sensor}: {i.Value}{i.Unit}"));
		return ApiResults.Ok(request, text, new RaspberryListResponse(info));
	}

	private static async Task<IResult> GetInfo(string info, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(info, out ERaspberryInfo parsed)) return ApiResults.UnknownValue<ERaspberryInfo>(request, "Raspberry info", info);

		StringResult result = await HardwareApi.Raspberry.GetHardwareInfo(parsed);
		return ApiResults.From(request, result, value =>
		{
			string text = parsed == ERaspberryInfo.Temperature
				? Helper.FormatTemperature(double.Parse(value, CultureInfo.InvariantCulture))
				: value;
			return ($"{parsed}: {text}", new SensorResponse(parsed.ToString(), value, Helper.UnitFor(parsed)));
		});
	}

	private static async Task<IResult> Command(PostCommand command, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(command.Command, out ERaspberryCommand parsed))
			return ApiResults.UnknownValue<ERaspberryCommand>(request, "Command", command.Command);

		StringResult result = parsed == ERaspberryCommand.Command
			? await HardwareApi.Raspberry.RunCommand(command.Args)
			: HardwareApi.Raspberry.Command(parsed);

		return ApiResults.From(request, result);
	}
}
