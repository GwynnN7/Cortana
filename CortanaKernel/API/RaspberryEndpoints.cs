using Carter;
using CortanaKernel.Hardware;
using CortanaKernel.Hardware.Structures;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CortanaKernel.API;

public class RaspberryEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("raspberry/");
		
		group.MapGet("", Root);
		group.MapGet("{info}", GetInfo);
		group.MapPost("{command}", Command);
	}

	private static Ok<string> Root()
	{
		return TypedResults.Ok("Raspberry API");
	}

	private static StringOrNotFoundResult GetInfo(string info)
	{
		IOption<ERaspberryInfo> cmd = info.ToEnum<ERaspberryInfo>();

		Result<string, string> result = cmd.Match(
			onSome: HardwareApi.Raspberry.GetHardwareInfo,
			onNone: () => StringResult.Failure("Raspberry information not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
	
	private static StringOrNotFoundResult Command(string command, [FromBody] string? argument)
	{
		IOption<ERaspberryCommand> cmd = command.ToEnum<ERaspberryCommand>();

		Result<string, string> result = cmd.Match(
			onSome: HardwareApi.Raspberry.Command,
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
}