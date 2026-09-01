using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.Api;

/// Every route can answer as plain text for the terminal and the bots, or as JSON for the dashboard
internal static class ApiResults
{
	private const string PlainText = "text/plain";

	public static bool WantsText(HttpRequest request)
	{
		StringValues accept = request.Headers.Accept;
		return accept.Contains(PlainText);
	}

	public static IResult Ok(HttpRequest request, string text, object json) =>
		WantsText(request)
			? TypedResults.Text(text, PlainText)
			: TypedResults.Json(json, CortanaEnvironment.WireJson);

	public static IResult Message(HttpRequest request, string message) => Ok(request, message, new MessageResponse(message));

	public static IResult Json(object value) => TypedResults.Json(value, CortanaEnvironment.WireJson);

	public static IResult BadRequest(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status400BadRequest, "Invalid request", detail);

	public static IResult NotFound(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status404NotFound, "Not found", detail);

	public static IResult Unavailable(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status503ServiceUnavailable, "Unavailable", detail);

	public static IResult Problem(HttpRequest request, int status, string title, string detail) =>
		WantsText(request)
			? TypedResults.Text(detail, PlainText, statusCode: status)
			: TypedResults.Json(new ProblemResponse(title, status, detail, request.Path),
				CortanaEnvironment.WireJson, contentType: "application/problem+json", statusCode: status);

	public static IResult From(HttpRequest request, Result<string> result) =>
		From(request, result, value => (value, new MessageResponse(value)));

	public static IResult From(HttpRequest request, Result<string> result, Func<string, (string Text, object Json)> onSuccess) =>
		result.Match(
			value =>
			{
				(string text, object json) = onSuccess(value);
				return Ok(request, text, json);
			},
			error => Failure(request, error));

	public static IResult From<T>(HttpRequest request, Result<T> result, Func<T, string> text) =>
		result.Match(value => Ok(request, text(value), value!), error => Failure(request, error));

	private static IResult Failure(HttpRequest request, string error) =>
		error.Contains("offline", StringComparison.OrdinalIgnoreCase) ||
		error.Contains("not connected", StringComparison.OrdinalIgnoreCase) ||
		error.Contains("is off", StringComparison.OrdinalIgnoreCase)
			? Unavailable(request, error)
			: BadRequest(request, error);

	public static bool TryParse<T>(string? value, out T parsed) where T : struct, Enum =>
		Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

	public static IResult Unknown<T>(HttpRequest request, string label, string? value) where T : struct, Enum =>
		NotFound(request, $"{label} '{value}' not found. Valid values: {string.Join(", ", Enum.GetNames<T>())}");
}
