using CortanaWeb.Components;
using Kernel.Hardware;
using Scalar.AspNetCore;

namespace CortanaWeb;

public static class CortanaWeb
{
    private static readonly WebApplication CortanaWebApi;
    
    static CortanaWeb()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        
        builder.Services.AddOpenApi("api"); 
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddFilter("Microsoft", LogLevel.None)
                .AddFilter("System", LogLevel.None)
                .AddFilter("NToastNotify", LogLevel.None)
                .AddConsole();
        });

        CortanaWebApi = builder.Build();
    }
    
    public static async Task BootCortanaWeb()
    {
        CortanaWebApi.Urls.Add($"https://*:{HardwareApi.NetworkData.ApiPort}");
        CortanaWebApi.Urls.Add($"http://*:{HardwareApi.NetworkData.ApiPort + 1}");
        CortanaWebApi.MapOpenApi();
        CortanaWebApi.MapScalarApiReference();
        CortanaWebApi.UseHttpsRedirection();
        
        CortanaWebApi.MapGet("/api", () => "Hi, I'm Cortana!")
            .WithName("Cortana");
        
        CortanaWebApi.UseAntiforgery();

        CortanaWebApi.MapStaticAssets();
        CortanaWebApi.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        
        await CortanaWebApi.RunAsync();
    }

    public static async Task StopCortanaWeb()
    {
        await CortanaWebApi.StopAsync();
        Console.WriteLine("Cortana Web shut down");
    }
}