using CortanaLib;
using CortanaLib.Structures;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

internal static class ApiResults
{
	private const string PlainText = "text/plain";

		public static bool WantsText(HttpRequest request)
	{
		StringValues accept = request.Headers.Accept;
		return accept.Contains(PlainText);
	}

	public static IResult Ok(HttpRequest request, string text, IApiResponse json) =>
		WantsText(request)
			? TypedResults.Text(text, PlainText)
			: TypedResults.Json(json, DataHandler.ApiSerializerOptions);

	public static IResult Message(HttpRequest request, string message) =>
		Ok(request, message, new MessageResponse(message));

	public static IResult BadRequest(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status400BadRequest, "Invalid request", detail);

	public static IResult NotFound(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status404NotFound, "Not found", detail);

		public static IResult Unavailable(HttpRequest request, string detail) =>
		Problem(request, StatusCodes.Status503ServiceUnavailable, "Unavailable", detail);

	public static IResult Problem(HttpRequest request, int status, string title, string detail) =>
		WantsText(request)
			? TypedResults.Text(detail, PlainText, statusCode: status)
			: TypedResults.Json(new ProblemDetails
			{
				Status = status,
				Title = title,
				Detail = detail,
				Instance = request.Path
			}, DataHandler.ApiSerializerOptions, contentType: "application/problem+json", statusCode: status);

		public static IResult From(HttpRequest request, StringResult result, Func<string, (string Text, IApiResponse Json)> onSuccess) =>
		result.Match(
			value =>
			{
				(string text, IApiResponse json) = onSuccess(value);
				return Ok(request, text, json);
			},
			error => Failure(request, error));

	public static IResult From(HttpRequest request, StringResult result) =>
		From(request, result, value => (value, new MessageResponse(value)));

		private static IResult Failure(HttpRequest request, string error) =>
		error.Contains("offline", StringComparison.OrdinalIgnoreCase) ||
		error.Contains("not connected", StringComparison.OrdinalIgnoreCase) ||
		error.Contains("is off", StringComparison.OrdinalIgnoreCase)
			? Unavailable(request, error)
			: BadRequest(request, error);

	public static bool TryParseEnum<T>(string? value, out T parsed) where T : struct, Enum =>
		Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

	public static IResult UnknownValue<T>(HttpRequest request, string label, string? value) where T : struct, Enum =>
		NotFound(request, $"{label} '{value}' not found. Valid values: {string.Join(", ", Enum.GetNames<T>())}");
}

public sealed record ProblemDetails
{
	public string Type { get; init; } = "about:blank";
	public required string Title { get; init; }
	public required int Status { get; init; }
	public required string Detail { get; init; }
	public string? Instance { get; init; }
}
