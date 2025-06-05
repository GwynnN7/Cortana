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
		if (acceptHeader.Contains("text/plain")) {
			return TypedResults.Text("Hi, I'm Cortana", "text/plain");
		}
		return TypedResults.Json(new MessageResponse(Message: "Hi, I'm Cortana"));
	}
}