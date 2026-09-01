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
				string text = string.Join("\n", all.Select(view => $"{view.Sensor}: {(view.Available ? view.Value + view.Unit : "offline")}"));
				if (sensors.CalibrationNote() is { Length: > 0 } note) text += $"\n\n{note}";

				return ApiResults.Ok(request, text, all);
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Latest reading from every sensor.").Produces<IReadOnlyList<SensorView>>();

		group.MapGet("/{sensor}", (string sensor, SensorService sensors, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(sensor, out SensorId parsed)) return ApiResults.Unknown<SensorId>(request, "Sensor", sensor);

				return ApiResults.From(request, sensors.Read(parsed),
					value => (sensors.Describe(parsed), new SensorView(parsed, value, CortanaLib.Runtime.Units.For(parsed), true, null)));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Latest reading from one sensor.").Produces<SensorView>();
	}
}
