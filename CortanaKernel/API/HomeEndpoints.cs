using Carter;
using CortanaLib;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public class HomeEndpoints : ICarterModule
{
	public void AddRoutes(IEndpointRouteBuilder app)
	{
		app.MapGet("", Root);
	}
	
	private static IResult Root(HttpRequest request)
	{
		StringValues acceptHeader = request.Headers.Accept;
		return acceptHeader.Contains("text/plain") ? 
			TypedResults.Text("Hi, I'm Cortana", "text/plain") : 
			TypedResults.Json(new MessageResponse(Message: "Hi, I'm Cortana"));
	}
}