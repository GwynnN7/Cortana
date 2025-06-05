using AngleSharp.Text;
using Carter;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Utility;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class RaspberryEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.Raspberry}/");
		
		group.MapGet("", Root);
		group.MapGet("{info}", GetInfo);
		group.MapPost("", Command);
	}

	private static IResult Root(HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		if (acceptHeader.Contains("text/plain")) {
			return TypedResults.Text("Raspberry API", "text/plain");
		}
		return TypedResults.Json(new MessageResponse(Message: "Raspberry API"));
	}

	private static IResult GetInfo(string info, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<ERaspberryInfo> cmd = info.ToEnum<ERaspberryInfo>();

		ERaspberryInfo? raspberryInfo = null;
		StringResult result = cmd.Match(
			onSome: value =>
			{
				raspberryInfo = value;
				return HardwareApi.Raspberry.GetHardwareInfo(value);
			},
			onNone: () => StringResult.Failure("Raspberry information not found")
		);

		return result.Match<IResult>(
			val =>
			{
				if (acceptHeader.Contains("text/plain")) {
					var text = raspberryInfo switch
					{
						ERaspberryInfo.Temperature => Helper.FormatTemperature(val.ToDouble()),
						_ => val
					};
					return TypedResults.Text($"{raspberryInfo}: {text}", "text/plain");
				}
				return TypedResults.Json(new SensorResponse(Sensor: raspberryInfo.ToString(),  Value: val, Unit: raspberryInfo == ERaspberryInfo.Temperature ? "°C" : ""));
			},
			err =>
			{
				if (acceptHeader.Contains("text/plain")) {
					return TypedResults.Text(err, "text/plain");
				}
				return TypedResults.Json(new ErrorResponse(Error: err));
			});
	}
	
	private static IResult Command(PostCommand command, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<ERaspberryCommand> cmd = command.Command.ToEnum<ERaspberryCommand>();

		StringResult result = cmd.Match(
			onSome: HardwareApi.Raspberry.Command,
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<IResult>(
			val =>
			{
				if (acceptHeader.Contains("text/plain")) {
					return TypedResults.Text(val, "text/plain");
				}
				return TypedResults.Json(new MessageResponse(Message: val));
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