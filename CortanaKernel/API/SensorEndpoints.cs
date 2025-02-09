using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class SensorEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
    	RouteGroupBuilder group = app.MapGroup($"{ERoute.Sensors}/");
    	
        group.MapGet("", Root);
        group.MapGet("{sensor}", GetData);
    }

    private static Ok<ResponseMessage> Root()
    {
    	return TypedResults.Ok(new ResponseMessage("Sensors API"));
    }
        
    private static StringOrFail GetData(string sensor)
    {
        IOption<ESensor> cmd = sensor.ToEnum<ESensor>();

        StringResult result = cmd.Match(
            onSome: HardwareApi.Sensors.GetData,
            onNone: () => StringResult.Failure("Sensor offline")
        );

        return result.Match<StringOrFail>(
            val => TypedResults.Ok(new ResponseMessage(val)),
            err => TypedResults.BadRequest(new ResponseMessage(err))
        );
    }
}