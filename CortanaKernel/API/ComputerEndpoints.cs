using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class ComputerEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.Computer}/");
		
		group.MapGet("", Root);
		group.MapPost("", Command);
	}

	private static Ok<ResponseMessage> Root()
	{
		return TypedResults.Ok(new ResponseMessage("Computer API"));
	}

	private static StringOrFail Command(PostCommand command)
	{
		IOption<EComputerCommand> cmd = command.Command.ToEnum<EComputerCommand>();

		StringResult result = cmd.Match(
			onSome: value => HardwareApi.Devices.CommandComputer(value, command.Args),
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrFail>(
			val => TypedResults.Ok(new ResponseMessage(val)),
			err => TypedResults.BadRequest(new ResponseMessage(err))
		);
	}
}