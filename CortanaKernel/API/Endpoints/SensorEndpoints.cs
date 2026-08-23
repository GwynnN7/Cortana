using System.Globalization;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class SensorEndpoints
{
	public static void MapSensorEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Sensors}").WithTags("Sensors");

		group.MapGet("", AllSensors)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSensors")
			.WithSummary("Latest reading from every sensor.")
			.Produces<SensorListResponse>();

		group.MapGet("/{sensor}", GetData)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSensor")
			.WithSummary("Latest reading from one sensor.")
			.Produces<SensorResponse>();
	}

	private static IResult AllSensors(HttpRequest request)
	{
		IReadOnlyList<SensorResponse> sensors = HardwareApi.Sensors.GetAllData();
		string text = string.Join("\n", sensors.Select(s => $"{s.Sensor}: {(s.Value.Length == 0 ? "offline" : s.Value + s.Unit)}"));
		return ApiResults.Ok(request, text, new SensorListResponse(sensors));
	}

	private static IResult GetData(string sensor, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(sensor, out ESensor parsed)) return ApiResults.UnknownValue<ESensor>(request, "Sensor", sensor);

		return ApiResults.From(request, HardwareApi.Sensors.GetData(parsed), value =>
		{
			string text = parsed switch
			{
				ESensor.Temperature => Helper.FormatTemperature(double.Parse(value, CultureInfo.InvariantCulture)),
				ESensor.Motion => value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "Motion Detected" : "Motion Not Detected",
				_ => $"{value}{Helper.UnitFor(parsed)}"
			};
			return (text, new SensorResponse(parsed.ToString(), value, Helper.UnitFor(parsed)));
		});
	}
}
