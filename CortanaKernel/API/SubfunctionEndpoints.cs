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
	    if (acceptHeader.Contains("text/plain")) {
		    return TypedResults.Text("Subfunctions API", "text/plain");
	    }
	    return TypedResults.Json(new MessageResponse(Message: "Subfunctions API"));
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
    
    private static IResult SubFunctionStatus(string subfunction, HttpRequest request)
    {
	    StringValues acceptHeader = request.Headers.Accept;
	    IOption<ESubFunctionType> settings = subfunction.ToEnum<ESubFunctionType>();

	    StringResult result = settings.Match(
		    onSome: val => StringResult.Success(Bootloader.IsSubfunctionRunning(val) ? $"{val} is running!" : $"{val} is not running."),
		    onNone: () => StringResult.Failure("Subfunction not found")
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
    
    private static IResult HandleSubfunction(string subfunction, PostAction command, HttpRequest request)
    {
	    StringValues acceptHeader = request.Headers.Accept;
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