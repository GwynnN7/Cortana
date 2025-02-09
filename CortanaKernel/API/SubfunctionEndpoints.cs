using Carter;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

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
    
    private static Ok<string> Root()
    {
    	return TypedResults.Ok("SubFunctions API");
    }
    
    private static StringOrFail PublishMessage(PostCommand message)
    {
	    IOption<EMessageCategory> category = message.Command.ToEnum<EMessageCategory>();

	    StringResult result = category.Match(
		    onSome: value =>
		    {
			     IpcService.Publish(value, string.IsNullOrEmpty(message.Args) ? "Hi, I'm Cortana" : message.Args);
			     return StringResult.Success("Message published!");
		    },
		    onNone: () => StringResult.Failure("Command not found")
	    );

	    return result.Match<StringOrFail>(
		    val => TypedResults.Ok(new ResponseMessage(val)),
		    err => TypedResults.BadRequest(new ResponseMessage(err))
	    );
    }
    
    private static StringOrFail SubFunctionStatus(string subfunction)
    {
	    IOption<ESubFunctionType> settings = subfunction.ToEnum<ESubFunctionType>();

	    StringResult result = settings.Match(
		    onSome: val => StringResult.Success(Bootloader.IsSubfunctionRunning(val) ? $"{val} is running!" : $"{val} is not running."),
		    onNone: () => StringResult.Failure("Subfunction not found")
	    );

	    return result.Match<StringOrFail>(
		    val => TypedResults.Ok(new ResponseMessage(val)),
		    err => TypedResults.BadRequest(new ResponseMessage(err))
	    );
    }
    
    private static StringOrFail HandleSubfunction(string subfunction, PostAction command)
    {
	    IOption<ESubFunctionType> func = subfunction.ToEnum<ESubFunctionType>();
	    IOption<ESubfunctionAction> action = command.Action.ToEnum<ESubfunctionAction>();

	    StringResult result = func.Match(
		    onSome: val =>
		    {
			    return action.Match(
				    onSome: act => Bootloader.SubfunctionCall(val, act).Result,
				    onNone: () => StringResult.Failure("Action not supported")
			    );
		    },
		    onNone: () => StringResult.Failure("Subfunction not found")
	    );

	    return result.Match<StringOrFail>(
		    val => TypedResults.Ok(new ResponseMessage(val)),
		    err => TypedResults.BadRequest(new ResponseMessage(err))
	    );
    }
}