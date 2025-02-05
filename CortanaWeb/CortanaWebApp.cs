using CortanaLib;
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
        
        builder.Services.AddScoped<HttpClient>(_ => new HttpClient { BaseAddress = new Uri(DataHandler.Env("CORTANA_API")) }); 
     
        WebApplication app = builder.Build();

        app.UseHttpsRedirection();
        app.UseAntiforgery();
        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        _ = Task.Run(async () =>
        {
            await SignalHandler.WaitForInterrupt();
            await app.StopAsync();
        });
        
        await app.RunAsync();
    }
}