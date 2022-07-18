using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CortanaAPI
{
    public static class CortanaAPI
    {
        private static WebApplication? CortanaWebAPI;
        public static void BootCortanaAPI()
        {
            var builder = WebApplication.CreateBuilder(new[] { "--urls=http://192.168.1.117:5000/" });

            Assembly RequestsHandlerAssemby = Assembly.Load(new AssemblyName("CortanaAPI"));
            builder.Services.AddMvc().AddApplicationPart(RequestsHandlerAssemby);
            builder.Services.AddControllers();
            builder.Services.AddLogging(builder =>
            {
                builder.AddFilter("Microsoft", LogLevel.None)
                       .AddFilter("System", LogLevel.None)
                       .AddFilter("NToastNotify", LogLevel.None)
                       .AddConsole();
            });
            
            CortanaWebAPI = builder.Build();
            CortanaWebAPI.UsePathBase("/cortana-api");
            CortanaWebAPI.UseAuthorization();
            CortanaWebAPI.MapControllers();

            CortanaWebAPI.Run();
        }
    }
}
