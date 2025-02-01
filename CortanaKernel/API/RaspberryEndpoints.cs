using Carter;
using CortanaKernel.Hardware;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

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

		StringResult result = cmd.Match(
			onSome: HardwareApi.Raspberry.GetHardwareInfo,
			onNone: () => StringResult.Failure("Raspberry information not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
	
	private static async Task<StringOrNotFoundResult> Command(string command, HttpContext context)
	{
		string arg = await new StreamReader(context.Request.Body).ReadToEndAsync();
		
		IOption<ERaspberryCommand> cmd = command.ToEnum<ERaspberryCommand>();

		StringResult result = cmd.Match(
			onSome: HardwareApi.Raspberry.Command,
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrNotFoundResult>(
			val => TypedResults.Ok(val),
			err => TypedResults.NotFound(err)
		);
	}
}