using Carter;
using CortanaKernel.Hardware;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class SensorEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
    	RouteGroupBuilder group = app.MapGroup("sensor/");
    	
        group.MapGet("", Root);
        group.MapGet("{sensor}", GetData);
        group.MapGet("settings", SettingsRoot);
        group.MapGet("settings/{setting}", GetSettings);
        group.MapPost("settings/{setting}", SetSettings);
    }

    private static Ok<string> Root()
    {
    	return TypedResults.Ok("Sensor API");
    }
    
    private static Ok<string> SettingsRoot()
    {
        return TypedResults.Ok("Sensor Settings API");
    }
        
    private static StringOrNotFoundResult GetData(string sensor)
    {
        IOption<ESensor> cmd = sensor.ToEnum<ESensor>();

        StringResult result = cmd.Match(
            onSome: HardwareApi.Sensors.GetData,
            onNone: () => StringResult.Failure("Sensor offline")
        );

        return result.Match<StringOrNotFoundResult>(
            val => TypedResults.Ok(val),
            err => TypedResults.NotFound(err)
        );
    }
    
    private static Results<Ok<int>, NotFound<string>> GetSettings(string setting)
    {
        IOption<ESensorSettings> settings = setting.ToEnum<ESensorSettings>();

        Result<int, string> result = settings.Match(
            onSome: HardwareApi.Sensors.GetSettings,
            onNone: () => Result<int, string>.Failure("Settings not found")
        );

        return result.Match<Results<Ok<int>, NotFound<string>>>(
            val => TypedResults.Ok(val),
            err => TypedResults.NotFound(err)
        );
    }
    
    private static async Task<Results<Ok<int>, NotFound<string>, BadRequest<string>>> SetSettings(string setting, HttpContext context)
    {
        string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
        if (!int.TryParse(body, out int value))
        {
            return TypedResults.BadRequest("Value not valid");
        }
        
        IOption<ESensorSettings> settings = setting.ToEnum<ESensorSettings>();

        Result<int, string> result = settings.Match(
            onSome: val => HardwareApi.Sensors.SetSettings(val, value),
            onNone: () => Result<int, string>.Failure("Settings not found")
        );

        return result.Match<Results<Ok<int>, NotFound<string>, BadRequest<string>>>(
            val => TypedResults.Ok(val),
            err => TypedResults.NotFound(err)
        );
    }
}