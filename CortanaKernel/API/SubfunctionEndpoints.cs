using Carter;
using CortanaLib;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class SubfunctionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) 
    	{
    		RouteGroupBuilder group = app.MapGroup($"{ERoute.SubFunction}/");
    		
    		group.MapGet("", Root);
    		group.MapGet("{subfunction}", SubFunctionStatus);
    		group.MapPost("{subfunction}", HandleSubfunction);
    	}
    	
    	private static Ok<string> Root()
    	{
    		return TypedResults.Ok("SubFunctions API");
    	}
    	
    	private static StringOrFail SubFunctionStatus(string subfunction)
    	{
		    return TypedResults.BadRequest(new ResponseMessage(subfunction));
    	}
    	
    	private static StringOrFail HandleSubfunction(string subfunction)
    	{
		    return TypedResults.BadRequest(new ResponseMessage(subfunction));
    	}
}