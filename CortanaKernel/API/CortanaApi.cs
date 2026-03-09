using Carter;
using CortanaLib;
using Scalar.AspNetCore;

namespace CortanaKernel.API;

public static class CortanaApi
{
    private static WebApplication _cortanaWebApi = null!;

    public static void Initialize()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();

        builder.Services.AddAuthorization();
        builder.Services.AddCarter();
        builder.Services.AddOpenApi();
        builder.Services.AddLogging(c => c.ClearProviders());
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowCors", policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        _cortanaWebApi = builder.Build();

        _cortanaWebApi.Urls.Add($"http://*:{DataHandler.Env("CORTANA_API_PORT")}");
        _cortanaWebApi.UseRouting();
        _cortanaWebApi.UseCors("AllowCors");

        string? apiKey = Environment.GetEnvironmentVariable("CORTANA_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            _cortanaWebApi.Use(async (context, next) =>
            {
                string path = context.Request.Path.Value ?? "";
                if (context.Request.Method == "OPTIONS" || path.StartsWith("/openapi") || path.StartsWith("/scalar"))
                {
                    await next();
                    return;
                }
                if (!context.Request.Headers.TryGetValue("X-Api-Key", out var key) || key != apiKey)
                {
                    context.Response.StatusCode = 401;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }
                await next();
            });
        }

        _cortanaWebApi.UseAuthorization();
        _cortanaWebApi.MapOpenApi();
        _cortanaWebApi.MapScalarApiReference();
        _cortanaWebApi.MapCarter();
    }

    public static async Task RunAsync()
    {
        await _cortanaWebApi.RunAsync();
    }

    public static async Task ShutdownService()
    {
        await _cortanaWebApi.StopAsync();
        await _cortanaWebApi.DisposeAsync();
        DataHandler.Log(nameof(CortanaApi), "API service interrupted.");
    }
}