using CortanaWeb.Components;

namespace CortanaWeb;

public class CortanaWebApp
{
    public static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", "http://*:5118");
        
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        
        builder.Services.AddLogging(c => c.ClearProviders());
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        
        WebApplication app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}