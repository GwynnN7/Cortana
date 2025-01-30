using CortanaWeb.Components;
using Kernel.Hardware;
using Scalar.AspNetCore;

namespace CortanaWeb;

public static class CortanaWeb
{
    private static readonly WebApplication CortanaWebApi;
    
    static CortanaWeb()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"https://*:{HardwareApi.NetworkData.ApiPort};http://*:{HardwareApi.NetworkData.ApiPort+1}");
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        
        WebApplicationOptions options = new WebApplicationOptions {
            ContentRootPath = "../../../../CortanaWeb"
        };
        
        WebApplicationBuilder builder = WebApplication.CreateBuilder(options);
        builder.WebHost.UseStaticWebAssets();
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
        CortanaWebApi.MapOpenApi();
        CortanaWebApi.MapScalarApiReference();
        CortanaWebApi.UseHttpsRedirection();
        
        CortanaWebApi.MapGet("/api", () => "Hi, I'm Cortana!")
            .WithName("Cortana");
        
        CortanaWebApi.UseAntiforgery();

        CortanaWebApi.MapStaticAssets("CortanaWeb.staticwebassets.endpoints.json");
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