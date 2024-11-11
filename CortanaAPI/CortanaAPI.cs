using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using System.Reflection;

namespace CortanaAPI
{
    public static class CortanaApi
    {
        private static WebApplication? _cortanaWebApi;
        public static void BootCortanaApi()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();

            Assembly requestsHandlerAssembly = Assembly.Load(new AssemblyName("CortanaAPI"));
            builder.Services.AddMvc().AddApplicationPart(requestsHandlerAssembly);
            builder.Services.AddControllers();
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddFilter("Microsoft", LogLevel.None)
                       .AddFilter("System", LogLevel.None)
                       .AddFilter("NToastNotify", LogLevel.None)
                       .AddConsole();
            });
            
            _cortanaWebApi = builder.Build();
            _cortanaWebApi.UseHttpsRedirection();
            _cortanaWebApi.UseStaticFiles(new StaticFileOptions 
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Storage/Assets")),
                RequestPath = "/resources"
            });

            _cortanaWebApi.UseAuthorization();
            _cortanaWebApi.MapControllers();

            _cortanaWebApi.Run("http://localhost::8080");
        }
    }
}
