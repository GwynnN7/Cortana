using Carter;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Structures;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CortanaKernel.API;

public class ComputerEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("computer/");
		
		group.MapGet("", Root);
		group.MapPost("{command}", Command);
	}

	private static Ok<string> Root()
	{
		return TypedResults.Ok("Computer API");
	}

	private static StringOrNotFoundResult Command(string command, [FromBody] string? argument)
	{
		IOption<EComputerCommand> cmd = command.ToEnum<EComputerCommand>();

		StringResult result = cmd.Match(
			onSome: value => HardwareApi.Devices.CommandComputer(value, argument),
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
}