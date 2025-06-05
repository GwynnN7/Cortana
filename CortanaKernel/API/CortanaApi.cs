global using StringOrFail = Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<CortanaLib.MessageResponse>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<CortanaLib.MessageResponse>>;
using Carter;
using CortanaLib;
using Scalar.AspNetCore;

namespace CortanaKernel.API;

public static class CortanaApi
{
    private static readonly WebApplication CortanaWebApi;
    static CortanaApi()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        
        builder.Services.AddAuthorization();
        builder.Services.AddCarter();
        builder.Services.AddLogging(c => c.ClearProviders());
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowCors", policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });
        
        CortanaWebApi = builder.Build();

        CortanaWebApi.Urls.Add($"http://*:{DataHandler.Env("CORTANA_API_PORT")}");
        CortanaWebApi.UseRouting();
        CortanaWebApi.UseCors("AllowCors");
        CortanaWebApi.UseAuthorization();
        CortanaWebApi.UseHttpsRedirection();
        CortanaWebApi.MapScalarApiReference();
        CortanaWebApi.MapCarter();
    }

    public static async Task RunAsync()
    {
        await CortanaWebApi.RunAsync();
    } 
    
    public static async Task ShutdownService()
    {
        await CortanaWebApi.StopAsync();
        await CortanaWebApi.DisposeAsync();
        Console.WriteLine("API service interrupted.");
    }
}