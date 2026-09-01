using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class SettingEndpoints
{
	public static void MapSettingEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/settings").WithTags("Settings");

		group.MapGet("", (SettingsService settings, HttpRequest request) =>
			{
				IReadOnlyList<SettingView> all = settings.All();
				return ApiResults.Ok(request, string.Join("\n", all.Select(view => $"{view.Setting}: {view.Value}{view.Unit}")), all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every automation setting.").Produces<IReadOnlyList<SettingView>>();

		group.MapGet("/{setting}", (string setting, SettingsService settings, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out SettingKey parsed)) return ApiResults.Unknown<SettingKey>(request, "Setting", setting);

				SettingView view = settings.Read(parsed);
				return ApiResults.Ok(request, $"{view.Setting}: {view.Value}{view.Unit}", view);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One automation setting.").Produces<SettingView>();

		group.MapPost("/{setting}", (string setting, SettingRequest body, SettingsService settings, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out SettingKey parsed)) return ApiResults.Unknown<SettingKey>(request, "Setting", setting);

				return ApiResults.From(request, settings.Write(parsed, body.Value),
					value => ($"{parsed}: {value}", settings.Read(parsed)));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Updates a setting. On/Off settings accept On, Off or Toggle.")
			.Produces<SettingView>();
	}
}
