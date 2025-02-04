global using StringOrFail = Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<CortanaLib.ResponseMessage>, Microsoft.AspNetCore.Http.HttpResults.BadRequest<CortanaLib.ResponseMessage>>;
using Carter;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Structures;
using Scalar.AspNetCore;

namespace CortanaKernel.API;

public static class CortanaApi
{
    private static readonly WebApplication CortanaWebApi;
    static CortanaApi()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi("api");
        builder.Services.AddCarter();
        builder.Services.AddLogging(c => c.ClearProviders());
        CortanaWebApi = builder.Build();

        CortanaWebApi.Urls.Add($"http://*:{DataHandler.Env("CORTANA_API_PORT")}");
        CortanaWebApi.MapOpenApi();
        CortanaWebApi.MapScalarApiReference();
        CortanaWebApi.UseHttpsRedirection();
        CortanaWebApi.UseAuthorization();
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