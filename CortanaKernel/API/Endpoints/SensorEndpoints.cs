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

		group.MapGet("/settings", AllSettings)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSensorSettings")
			.WithSummary("Every automation setting and its current value.")
			.Produces<SettingsListResponse>();

		group.MapGet("/settings/{setting}", GetSetting)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSensorSetting")
			.WithSummary("One automation setting.")
			.Produces<SettingsResponse>();

		group.MapPost("/settings/{setting}", SetSetting)
			.Access(EApiAccess.Sensitive)
			.WithName("SetSensorSetting")
			.WithSummary("Updates a setting. On/Off settings toggle when given any other number.")
			.Produces<SettingsResponse>();

		group.MapGet("/{sensor}", GetData)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSensor")
			.WithSummary("Latest reading from one sensor.")
			.Produces<SensorResponse>();
	}

	private static IResult AllSettings(HttpRequest request)
	{
		IReadOnlyList<SettingsResponse> settings = HardwareApi.Sensors.GetAllSettings()
			.Where(setting => !Enum.Parse<ESettings>(setting.Setting).IsLog()).ToList();

		string text = string.Join("\n", settings.Select(setting => $"{setting.Setting}: {setting.Value}"));
		return ApiResults.Ok(request, text, new SettingsListResponse(settings));
	}

	private static IResult GetSetting(string setting, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out ESettings parsed) || parsed.IsLog())
			return ApiResults.UnknownValue<ESettings>(request, "Setting", setting);

		return ApiResults.From(request, HardwareApi.Sensors.GetSettings(parsed),
			value => ($"{parsed}: {value}", new SettingsResponse(parsed.ToString(), value)));
	}

	private static IResult SetSetting(string setting, PostValue value, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out ESettings parsed) || parsed.IsLog())
			return ApiResults.UnknownValue<ESettings>(request, "Setting", setting);

		return ApiResults.From(request, HardwareApi.Sensors.SetSettings(parsed, value.Value),
			updated => ($"{parsed}: {updated}", new SettingsResponse(parsed.ToString(), updated)));
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
