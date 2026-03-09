using Carter;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class SubfunctionEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"{ERoute.SubFunctions}/");

		group.MapGet("", Root);
		group.MapPost("", PublishMessage);
		group.MapGet("{subfunction}", SubFunctionStatus);
		group.MapPost("{subfunction}", HandleSubfunction);
	}

	private static IResult Root(HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		return acceptHeader.Contains("text/plain") ?
			TypedResults.Text("Subfunctions API", "text/plain") :
			TypedResults.Json(new MessageResponse(Message: "Subfunctions API"));
	}

	private static IResult PublishMessage(PostCommand message, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<EMessageCategory> category = message.Command.ToEnum<EMessageCategory>();

		StringResult result = category.Match(
			onSome: value =>
			{
				IpcService.Publish(value, string.IsNullOrEmpty(message.Args) ? "Hi, I'm Cortana" : message.Args);
				return StringResult.Success("Message published!");
			},
			onNone: () => StringResult.Failure("Command not found")
		);

		return result.Match<IResult>(
			val => acceptHeader.Contains("text/plain") ?
				TypedResults.Text(val, "text/plain") :
				TypedResults.Json(new MessageResponse(Message: val)),
			err => acceptHeader.Contains("text/plain") ?
				TypedResults.BadRequest(err) :
				TypedResults.BadRequest(new ErrorResponse(Error: err))
		);
	}

	private static async Task<IResult> SubFunctionStatus(string subfunction, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<ESubFunctionType> settings = subfunction.ToEnum<ESubFunctionType>();

		StringResult result = await settings.Match<Task<StringResult>>(
			onSome: async val => StringResult.Success(await Bootloader.IsSubfunctionRunning(val) ? $"{val} is running!" : $"{val} is not running."),
			onNone: () => Task.FromResult(StringResult.Failure("Subfunction not found"))
		);

		return result.Match<IResult>(
			val => acceptHeader.Contains("text/plain") ?
				TypedResults.Text(val, "text/plain") :
				TypedResults.Json(new MessageResponse(Message: val)),
			err => acceptHeader.Contains("text/plain") ?
				TypedResults.BadRequest(err) :
				TypedResults.BadRequest(new ErrorResponse(Error: err))
		);
	}

	private static async Task<IResult> HandleSubfunction(string subfunction, PostAction command, HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		IOption<ESubFunctionType> func = subfunction.ToEnum<ESubFunctionType>();
		IOption<ESubfunctionAction> action = command.Action.ToEnum<ESubfunctionAction>();

		StringResult result = await func.Match<Task<StringResult>>(
			onSome: val => action.Match<Task<StringResult>>(
				onSome: act => Bootloader.SubfunctionCall(val, act),
				onNone: () => Task.FromResult(StringResult.Failure("Action not supported"))
			),
			onNone: () => Task.FromResult(StringResult.Failure("Subfunction not found"))
		);

		return result.Match<IResult>(
			val => acceptHeader.Contains("text/plain") ?
				TypedResults.Text(val, "text/plain") :
				TypedResults.Json(new MessageResponse(Message: val)),
			err => acceptHeader.Contains("text/plain") ?
				TypedResults.BadRequest(err) :
				TypedResults.BadRequest(new ErrorResponse(Error: err))
		);
	}
}