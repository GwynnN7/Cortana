using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

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

    private static IResult Root(HttpRequest request)
    {
        StringValues acceptHeader = request.Headers.Accept;
        if (acceptHeader.Contains("text/plain")) {
            return TypedResults.Text("Settings API", "text/plain");
        }
        return TypedResults.Json(new MessageResponse(Message: "Settings API"));
    }
    
    private static IResult GetSettings(string setting, HttpRequest request)
    {
        StringValues acceptHeader = request.Headers.Accept;
        IOption<ESettings> settings = setting.ToEnum<ESettings>();

        ESettings? settingType = null;
        StringResult result = settings.Match(
            onSome: value =>
            {
                settingType = value;
                return HardwareApi.Sensors.GetSettings(value);
            },
            onNone: () => StringResult.Failure("Settings not found")
        );

        return result.Match<IResult>(
            val =>
            {
                if (acceptHeader.Contains("text/plain")) {
                    return TypedResults.Text($"{settingType}: {val}", "text/plain");
                }
                return TypedResults.Json(new SettingsResponse(Setting: settingType.ToString(),  Value: val));
            },
            err =>
            {
                if (acceptHeader.Contains("text/plain")) {
                    return TypedResults.Text(err, "text/plain");
                }
                return TypedResults.Json(new ErrorResponse(Error: err));
            });
    }
    
    private static IResult SetSettings(string setting, PostValue value, HttpRequest request)
    {
        StringValues acceptHeader = request.Headers.Accept;
        IOption<ESettings> settings = setting.ToEnum<ESettings>();

        ESettings? settingType = null;
        StringResult result = settings.Match(
            onSome: val =>
            {
                settingType = val;
                return HardwareApi.Sensors.SetSettings(val, value.Value);
            },
            onNone: () => StringResult.Failure("Settings not found")
        );

        return result.Match<IResult>(
            val =>
            {
                if (acceptHeader.Contains("text/plain")) {
                    return TypedResults.Text($"{settingType}: {val}", "text/plain");
                }
                return TypedResults.Json(new SettingsResponse(Setting: settingType.ToString(),  Value: val));
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