using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class SettingsEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
    	RouteGroupBuilder group = app.MapGroup($"{ERoute.Settings}/");
    	
        group.MapGet("", Root);
        group.MapGet("{setting}", GetSettings);
        group.MapPost("{setting}", SetSettings);
    }

    private static Ok<ResponseMessage> Root()
    {
    	return TypedResults.Ok(new ResponseMessage("Settings API"));
    }
    
    private static StringOrFail GetSettings(string setting)
    {
        IOption<ESettings> settings = setting.ToEnum<ESettings>();

        Result<int, string> result = settings.Match(
            onSome: HardwareApi.Sensors.GetSettings,
            onNone: () => Result<int, string>.Failure("Settings not found")
        );

        return result.Match<StringOrFail>(
            val => TypedResults.Ok(new ResponseMessage(val.ToString())),
            err => TypedResults.BadRequest(new ResponseMessage(err))
        );
    }
    
    private static StringOrFail SetSettings(string setting, PostValue value)
    {
       
        IOption<ESettings> settings = setting.ToEnum<ESettings>();

        Result<int, string> result = settings.Match(
            onSome: val => HardwareApi.Sensors.SetSettings(val, value.Value),
            onNone: () => Result<int, string>.Failure("Settings not found")
        );

        return result.Match<StringOrFail>(
            val => TypedResults.Ok(new ResponseMessage(val.ToString())),
            err => TypedResults.BadRequest(new ResponseMessage(err))
        );
    }
}