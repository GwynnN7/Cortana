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
        return acceptHeader.Contains("text/plain") ?
            TypedResults.Text("Settings API", "text/plain") :
            TypedResults.Json(new MessageResponse(Message: "Settings API"));
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
            val => acceptHeader.Contains("text/plain") ?
                TypedResults.Text($"{settingType}: {val}", "text/plain") :
                TypedResults.Json(new SettingsResponse(Setting: settingType!.Value.ToString(), Value: val)),
            err => acceptHeader.Contains("text/plain") ?
                TypedResults.BadRequest(err) :
                TypedResults.BadRequest(new ErrorResponse(Error: err))
            );
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
            val => acceptHeader.Contains("text/plain") ?
                TypedResults.Text($"{settingType}: {val}", "text/plain") :
                TypedResults.Json(new SettingsResponse(Setting: settingType!.Value.ToString(), Value: val)),
            err => acceptHeader.Contains("text/plain") ?
                TypedResults.BadRequest(err) :
                TypedResults.BadRequest(new ErrorResponse(Error: err))
        );
    }
}