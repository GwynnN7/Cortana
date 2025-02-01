using Carter;
using CortanaKernel.Hardware;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

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

	private static async Task<StringOrNotFoundResult> Command(string command, HttpContext context)
	{
		string body = await new StreamReader(context.Request.Body).ReadToEndAsync();
		
		IOption<EComputerCommand> cmd = command.ToEnum<EComputerCommand>();

		StringResult result = cmd.Match(
			onSome: value => HardwareApi.Devices.CommandComputer(value, body),
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
}