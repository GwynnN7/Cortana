using Carter;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class HomeEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("", Root);
	}
	
	private static Ok<string> Root()
	{
		return TypedResults.Ok("Hi, I'm Cortana");
	}
}