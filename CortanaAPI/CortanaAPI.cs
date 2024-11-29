using System.Reflection;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CortanaAPI;

public static class CortanaApi
{
	private static readonly WebApplication CortanaWebApi;

	static CortanaApi()
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

		CortanaWebApi = builder.Build();
	}

	public static async Task BootCortanaApi()
	{
		CortanaWebApi.UseStaticFiles(new StaticFileOptions
		{
			FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory(), "Storage/Assets")),
			RequestPath = "/resources"
		});
		CortanaWebApi.Urls.Add("http://*:8080");
		CortanaWebApi.MapControllers();
		await CortanaWebApi.RunAsync();
	}

	public static async Task StopCortanaApi()
	{
		await CortanaWebApi.StopAsync();
		Console.WriteLine("Cortana API shut down");
	}
}