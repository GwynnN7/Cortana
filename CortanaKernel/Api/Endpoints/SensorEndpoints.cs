using CortanaKernel.Application;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class SensorEndpoints
{
	public static void MapSensorEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/sensors").WithTags("Sensors");

		group.MapGet("", (SensorService sensors, HttpRequest request) =>
			{
				IReadOnlyList<SensorView> all = sensors.All();
				string text = string.Join("\n", all.Select(view => $"{view.Name}: {(view.Available ? view.Value + view.Unit : "offline")}"));

				return ApiResults.Ok(request, text, all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Latest reading from every sensor").Produces<IReadOnlyList<SensorView>>();

		group.MapGet("/{sensor}", (string sensor, SensorService sensors, HttpRequest request) =>
			{
				SensorView? known = sensors.All().FirstOrDefault(view => view.Sensor.Equals(sensor, StringComparison.OrdinalIgnoreCase));
				if (known is null) return ApiResults.NotFound(request, $"Unknown sensor '{sensor}'");

				return ApiResults.Ok(request, sensors.Describe(sensor), known);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Latest reading from one sensor").Produces<SensorView>();
	}
}
