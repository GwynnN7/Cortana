using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

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

	private static Ok<ResponseMessage> Root()
	{
		return TypedResults.Ok(new ResponseMessage("Raspberry API"));
	}

	private static StringOrFail GetInfo(string info)
	{
		IOption<ERaspberryInfo> cmd = info.ToEnum<ERaspberryInfo>();

		StringResult result = cmd.Match(
			onSome: HardwareApi.Raspberry.GetHardwareInfo,
			onNone: () => StringResult.Failure("Raspberry information not found")
		);

		return result.Match<StringOrFail>(
			val => TypedResults.Ok(new ResponseMessage(val)),
			err => TypedResults.BadRequest(new ResponseMessage(err))
		);
	}
	
	private static StringOrFail Command(PostCommand command)
	{
	
		IOption<ERaspberryCommand> cmd = command.Command.ToEnum<ERaspberryCommand>();

		StringResult result = cmd.Match(
			onSome: HardwareApi.Raspberry.Command,
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<StringOrFail>(
			val => TypedResults.Ok(new ResponseMessage(val)),
			err => TypedResults.BadRequest(new ResponseMessage(err))
		);
	}
}