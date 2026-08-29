using Microsoft.Extensions.Logging;
using CortanaKernel.API.Endpoints;
using CortanaLib;
using Microsoft.Extensions.Primitives;
using Scalar.AspNetCore;

namespace CortanaKernel.API;

public static class CortanaApi
{
	private static WebApplication _api = null!;

	public static void Initialize()
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder();

		builder.Services.AddOpenApi();

		builder.Services.ConfigureHttpJsonOptions(options =>
		{
			options.SerializerOptions.PropertyNamingPolicy = DataHandler.ApiSerializerOptions.PropertyNamingPolicy;
			options.SerializerOptions.PropertyNameCaseInsensitive = true;
			foreach (System.Text.Json.Serialization.JsonConverter converter in DataHandler.ApiSerializerOptions.Converters)
				options.SerializerOptions.Converters.Add(converter);
		});
		builder.Services.AddLogging(c => { c.ClearProviders(); c.AddSimpleConsole(); c.SetMinimumLevel(LogLevel.Error); });
		builder.Services.AddCors(options => options.AddPolicy("AllowCors", policy =>
			policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

		_api = builder.Build();
		_api.Urls.Add($"http://*:{DataHandler.Env("CORTANA_API_PORT")}");

		_api.UseRouting();
		_api.UseCors("AllowCors");
		_api.Use(ApiKeyMiddleware(ApiKeyGate.FromEnvironment()));

		_api.MapOpenApi();
		_api.MapScalarApiReference();

		_api.MapHomeEndpoints();
		_api.MapDeviceEndpoints();
		_api.MapSensorEndpoints();
		
		_api.MapRaspberryEndpoints();
		_api.MapComputerEndpoints();
		_api.MapSubfunctionEndpoints();
		_api.MapScheduleEndpoints();
		_api.MapLogEndpoints();
		_api.MapAiEndpoints();
		_api.MapPushEndpoints();
		_api.MapHistoryEndpoints();
	}

		private static Func<HttpContext, RequestDelegate, Task> ApiKeyMiddleware(ApiKeyGate gate)
	{
		return async (context, next) =>
		{
			EApiAccess access = context.GetEndpoint()?.Metadata.GetMetadata<ApiAccessMetadata>()?.Access ?? EApiAccess.Public;

			if (access == EApiAccess.Public || HttpMethods.IsOptions(context.Request.Method))
			{
				await next(context);
				return;
			}

			if (!gate.IsConfigured)
			{
				await Deny(context, StatusCodes.Status503ServiceUnavailable,
					"CORTANA_API_KEY is not configured on the server, so this route is disabled.");
				return;
			}

			if (!context.Request.Headers.TryGetValue("X-Api-Key", out StringValues provided) || !gate.Matches(provided))
			{
				await Deny(context, StatusCodes.Status401Unauthorized, "A valid X-Api-Key header is required.");
				return;
			}

			await next(context);
		};
	}

	private static async Task Deny(HttpContext context, int status, string detail)
	{
		context.Response.StatusCode = status;

		if (ApiResults.WantsText(context.Request))
		{
			context.Response.ContentType = "text/plain";
			await context.Response.WriteAsync(detail);
			return;
		}

		context.Response.ContentType = "application/problem+json";
		await context.Response.WriteAsJsonAsync(new ProblemDetails
		{
			Status = status,
			Title = status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Unavailable",
			Detail = detail,
			Instance = context.Request.Path
		}, DataHandler.ApiSerializerOptions);
	}

	public static Task RunAsync() => _api.RunAsync();

	public static async Task ShutdownService()
	{
		await _api.StopAsync();
		await _api.DisposeAsync();
		DataHandler.Log("API service interrupted.");
	}
}
