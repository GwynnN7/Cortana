using AngleSharp.Text;
using Carter;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class SensorEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
    	RouteGroupBuilder group = app.MapGroup($"{ERoute.Sensors}/");
    	
        group.MapGet("", Root);
        group.MapGet("{sensor}", GetData);
    }

    private static IResult Root(HttpRequest request)
    {
        StringValues acceptHeader = request.Headers.Accept;
        if (acceptHeader.Contains("text/plain")) {
            return TypedResults.Text("Sensor API", "text/plain");
        }
        return TypedResults.Json(new MessageResponse(Message: "Sensor API"));
    }
        
    private static IResult GetData(string sensor, HttpRequest request)
    {
        StringValues acceptHeader = request.Headers.Accept;
        IOption<ESensor> cmd = sensor.ToEnum<ESensor>();

        ESensor? sensorType = null;
        StringResult result = cmd.Match(
            onSome: value =>
            {
                sensorType = value;
                return HardwareApi.Sensors.GetData(value);
            },
            onNone: () => StringResult.Failure("Sensor offline")
        );

        return result.Match<IResult>(
            val =>
            {
                if (acceptHeader.Contains("text/plain"))
                {
                    var text = sensorType switch
                    {
                        ESensor.Temperature => Helper.FormatTemperature(val.ToDouble()),
                        ESensor.Motion => val == true.ToString() ? "Motion Detected" : "Motion Not Detected",
                        _ => val
                    };
                    return TypedResults.Text(text, "text/plain");
                }
                return TypedResults.Json(new SensorResponse(Sensor: sensorType.ToString(),  Value: val, Unit: sensorType == ESensor.Temperature ? "°C" : ""));
            },
            err =>
            {
                if (acceptHeader.Contains("text/plain")) {
                    return TypedResults.Text(err, "text/plain");
                }
                return TypedResults.Json(new ErrorResponse(Error: err));
            });
    }
}