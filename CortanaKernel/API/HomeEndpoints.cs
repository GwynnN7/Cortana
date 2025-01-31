using Carter;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class HomeEndpoints : IEndpoint
{
	public static void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("", Root);
	}
	
	private static Ok<string> Root()
	{
		return TypedResults.Ok("Hi, I'm Cortana");
	}
}