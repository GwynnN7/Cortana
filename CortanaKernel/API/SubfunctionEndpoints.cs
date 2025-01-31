using Carter;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CortanaKernel.API;

public class SubfunctionEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app) 
    	{
    		RouteGroupBuilder group = app.MapGroup("subfunctions/");
    		
    		group.MapGet("", Root);
    		group.MapGet("{subfunction}", SubFunctionStatus);
    		group.MapPost("{subfunction}", HandleSubfunction);
    	}
    	
    	private static Ok<string> Root()
    	{
    		return TypedResults.Ok("SubFunctions API");
    	}
    	
    	private static StringOrNotFoundResult SubFunctionStatus(string subfunction)
    	{
		    return TypedResults.NotFound(subfunction);
    	}
    	
    	private static StringOrNotFoundResult HandleSubfunction(string subfunction, [FromBody] string? action)
    	{
		    return TypedResults.NotFound(subfunction);
    	}
}