using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class ComputerEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.Computer}/");
		
		group.MapGet("", Root);
		group.MapPost("", Command);
	}

	private static IResult Root(HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		if (acceptHeader.Contains("text/plain")) {
			return TypedResults.Text("Computer API", "text/plain");
		}
		return TypedResults.Json(new MessageResponse(Message: "Computer API"));
	}

	private static IResult Command(PostCommand command, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<EComputerCommand> cmd = command.Command.ToEnum<EComputerCommand>();

		StringResult result = cmd.Match(
			onSome: value => HardwareApi.Devices.CommandComputer(value, string.IsNullOrEmpty(command.Args) ? null : command.Args),
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