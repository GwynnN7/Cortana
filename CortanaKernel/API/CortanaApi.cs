global using StringOrNotFoundResult = Microsoft.AspNetCore.Http.HttpResults.Results<Microsoft.AspNetCore.Http.HttpResults.Ok<string>, Microsoft.AspNetCore.Http.HttpResults.NotFound<string>>;
using Carter;
using CortanaKernel.Hardware;
using CortanaLib.Structures;

namespace CortanaKernel.API;

public static class CortanaApi
{
    private static readonly WebApplication CortanaWebApi;
    static CortanaApi()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();
        builder.Services.AddCarter();
        CortanaWebApi = builder.Build();
        
        CortanaWebApi.Urls.Add($"http://*:{HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.ApiPort)}");
        Console.WriteLine(HardwareApi.Raspberry.GetHardwareInfo(ERaspberryInfo.ApiPort));
        CortanaWebApi.MapOpenApi();
        CortanaWebApi.UseHttpsRedirection();
        CortanaWebApi.UseAuthorization();
        CortanaWebApi.MapCarter();
        
        ComputerEndpoints.AddRoutes(CortanaWebApi);
        HomeEndpoints.AddRoutes(CortanaWebApi);
    }
    
    public static async Task RunAsync() => await CortanaWebApi.RunAsync();
    
    public static async Task ShutdownService()
    {
        await CortanaWebApi.StopAsync();
        Console.WriteLine("API service interrupted.");
    }
}