using Carter;
using CortanaLib;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CortanaKernel.API;

public class HomeEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("", Root);
	}
	
	private static Ok<ResponseMessage> Root()
	{
		return TypedResults.Ok(new ResponseMessage("Hi, I'm Cortana"));
	}
}