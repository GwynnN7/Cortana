using CortanaLib;
using CortanaLib.Structures;
using CortanaWeb.Components;

namespace CortanaWeb;

public class CortanaWebApp
{
    public static void Main(string[] args)
    {
        var settings = FileHandler.DeserializeJson<WebAppSettings>(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaWeb)}/WebAppSettings.json"));
        
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", settings.DevStatus);
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://*:{settings.Port}");
        
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddLogging(c => c.ClearProviders());
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        builder.Services.AddScoped<HttpClient>(_ => new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("CORTANA_API") ?? throw new CortanaException("Cortana API not set in env")) }); 
        
        WebApplication app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}