using CortanaKernel.Hardware;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class SettingsEndpoints
{
	public static void MapSettingsEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.Settings}").WithTags("Settings");

		group.MapGet("", AllSettings)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSettings")
			.WithSummary("Every automation setting and its current value.")
			.Produces<SettingsListResponse>();

		group.MapGet("/{setting}", GetSetting)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSetting")
			.WithSummary("One automation setting.")
			.Produces<SettingsResponse>();

		group.MapPost("/{setting}", SetSetting)
			.Access(EApiAccess.Sensitive)
			.WithName("SetSetting")
			.WithSummary("Updates a setting. On/Off settings toggle when given any other number.")
			.Produces<SettingsResponse>();
	}

	private static IResult AllSettings(HttpRequest request)
	{
		IReadOnlyList<SettingsResponse> settings = HardwareApi.Sensors.GetAllSettings();
		string text = string.Join("\n", settings.Select(s => $"{s.Setting}: {s.Value}"));
		return ApiResults.Ok(request, text, new SettingsListResponse(settings));
	}

	private static IResult GetSetting(string setting, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out ESettings parsed)) return ApiResults.UnknownValue<ESettings>(request, "Setting", setting);

		return ApiResults.From(request, HardwareApi.Sensors.GetSettings(parsed),
			value => ($"{parsed}: {value}", new SettingsResponse(parsed.ToString(), value)));
	}

	private static IResult SetSetting(string setting, PostValue value, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out ESettings parsed)) return ApiResults.UnknownValue<ESettings>(request, "Setting", setting);

		return ApiResults.From(request, HardwareApi.Sensors.SetSettings(parsed, value.Value),
			updated => ($"{parsed}: {updated}", new SettingsResponse(parsed.ToString(), updated)));
	}
}
