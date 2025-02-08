using CortanaWeb.Components;

namespace CortanaWeb;

public class CortanaWebApp
{
    public static async Task Main(string[] args)
    { 
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddLogging(c => c.ClearProviders());
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        WebApplication app = builder.Build();
        
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();
        await app.RunAsync();


    }
}