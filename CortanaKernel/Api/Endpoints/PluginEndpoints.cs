using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class PluginEndpoints
{
	public static void MapPlugins(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/plugins").WithTags("Plugins");

		group.MapGet("", (PluginService plugins, HttpRequest request) =>
			{
				IReadOnlyList<PluginView> all = plugins.All();

				string text = string.Join("\n", all.Select(view =>
					$"{(view.Active ? "on " : "off")} {view.Name}: {view.Purpose}{(view.Detail.Length > 0 ? $" ({view.Detail})" : "")}"));

				return ApiResults.Ok(request, text, new PluginListResponse(all));
			})
			.Access(ApiAccess.ReadOnly)
			.WithSummary("Every feature Cortana runs, and whether it is active").Produces<PluginListResponse>();

		group.MapPost("/{plugin}", (string plugin, SwitchRequest? body, PluginService plugins, HttpRequest request) =>
				ApiResults.From(request, plugins.Switch(plugin, body?.Action ?? SwitchAction.Toggle)))
			.Access(ApiAccess.Sensitive).WithSummary("Turns one feature on or off");
	}
}
