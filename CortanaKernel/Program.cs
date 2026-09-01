using CortanaKernel.Api;
using CortanaKernel.Api.Endpoints;
using CortanaKernel.Application;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Devices;
using CortanaKernel.Domain.History;
using CortanaKernel.Domain.Metrics;
using CortanaKernel.Domain.Notifications;
using CortanaKernel.Domain.Scheduling;
using CortanaKernel.Domain.Sensors;
using CortanaKernel.Domain.Services;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Infrastructure.Ai;
using CortanaKernel.Infrastructure.Gpio;
using CortanaKernel.Infrastructure.Network;
using CortanaKernel.Infrastructure.Persistence;
using CortanaKernel.Infrastructure.Process;
using CortanaKernel.Infrastructure.Push;
using CortanaKernel.Infrastructure.Raspberry;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Microsoft.Extensions.Primitives;
using Scalar.AspNetCore;

CortanaEnvironment.Load();
Log.Write("Kernel", "Booting");

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.PropertyNamingPolicy = CortanaEnvironment.WireJson.PropertyNamingPolicy;
	options.SerializerOptions.PropertyNameCaseInsensitive = true;
	foreach (System.Text.Json.Serialization.JsonConverter converter in CortanaEnvironment.WireJson.Converters)
		options.SerializerOptions.Converters.Add(converter);
});
builder.Services.AddLogging(logging => logging.ClearProviders().AddSimpleConsole().SetMinimumLevel(LogLevel.Error));
builder.Services.AddCors(options => options.AddPolicy("Cortana", policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

// ---------- domain ----------
builder.Services.AddSingleton<IEventBus, EventBus>();
builder.Services.AddSingleton<DeviceRegistry>();
builder.Services.AddSingleton<SensorRegistry>();
builder.Services.AddSingleton<MetricsRegistry>();
builder.Services.AddSingleton<NotificationLog>();
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<AiSettingsStore>();
builder.Services.AddSingleton<AutomationEngine>();

// ---------- infrastructure ----------
builder.Services.AddSingleton<RaspberryHost>();
builder.Services.AddSingleton<IHostMachine>(provider => provider.GetRequiredService<RaspberryHost>());
builder.Services.AddSingleton<ILocalDeviceController, GpioDeviceController>();
builder.Services.AddSingleton<IServiceSupervisor, SystemdSupervisor>();
builder.Services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
builder.Services.AddSingleton<IAiSettingsRepository, JsonAiSettingsRepository>();
builder.Services.AddSingleton<IScheduleRepository, JsonScheduleRepository>();
builder.Services.AddSingleton<IConversationRepository, JsonConversationRepository>();
builder.Services.AddSingleton<IHistoryRepository, CsvHistoryRepository>();
builder.Services.AddSingleton<DesktopComputerEndpoint>();
builder.Services.AddSingleton<IComputerEndpoint>(provider => provider.GetRequiredService<DesktopComputerEndpoint>());
builder.Services.AddSingleton<Esp32SensorSource>();
builder.Services.AddSingleton<ModelCatalogue>();
builder.Services.AddSingleton<IAiProvider, GeminiProvider>();
builder.Services.AddSingleton<PushService>();
builder.Services.AddSingleton(provider => new Lazy<PushService>(provider.GetRequiredService<PushService>));

// ---------- application ----------
builder.Services.AddSingleton<StateBroadcaster>();
builder.Services.AddSingleton<IComputerPresence, ComputerPresenceService>();
builder.Services.AddSingleton<IAutomationWorld, AutomationWorld>();
builder.Services.AddSingleton<IAutomationEffects, AutomationEffects>();
builder.Services.AddSingleton(provider => new Lazy<DeviceService>(provider.GetRequiredService<DeviceService>));
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<DeviceService>();
builder.Services.AddSingleton<SensorService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<ServiceControlService>();
builder.Services.AddSingleton<SnapshotService>();
builder.Services.AddSingleton<CapabilityRegistry>();
builder.Services.AddSingleton<AiService>();

builder.Services.AddSingleton<INotificationSink, WebPushSink>();
builder.Services.AddSingleton<INotificationSink>(provider =>
	new StreamNotificationSink(NotificationChannel.Telegram, provider.GetRequiredService<StateBroadcaster>()));
builder.Services.AddSingleton<INotificationSink>(provider =>
	new StreamNotificationSink(NotificationChannel.Discord, provider.GetRequiredService<StateBroadcaster>()));

// ---------- long-running work ----------
builder.Services.AddSingleton<AutomationService>();
builder.Services.AddSingleton<ScheduleService>();
builder.Services.AddSingleton<MetricsService>();
builder.Services.AddSingleton<HistoryService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<MetricsService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<AutomationService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<ScheduleService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<HistoryService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<ModelCatalogue>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<PushService>());
builder.Services.AddHostedService<ConnectionServer>();

WebApplication app = builder.Build();
app.Urls.Add($"http://*:{CortanaEnvironment.Require("CORTANA_API_PORT")}");

ApiKeyGate gate = ApiKeyGate.FromEnvironment();

app.UseRouting();
app.UseCors("Cortana");
app.Use(async (context, next) =>
{
	ApiAccess access = context.GetEndpoint()?.Metadata.GetMetadata<ApiAccessMetadata>()?.Access ?? ApiAccess.Public;

	if (access == ApiAccess.Public || HttpMethods.IsOptions(context.Request.Method))
	{
		await next(context);
		return;
	}

	if (!gate.IsConfigured)
	{
		await Deny(context, StatusCodes.Status503ServiceUnavailable, "CORTANA_API_KEY is not configured on the server, so this route is disabled");
		return;
	}

	if (!context.Request.Headers.TryGetValue("X-Api-Key", out StringValues provided) || !gate.Matches(provided))
	{
		await Deny(context, StatusCodes.Status401Unauthorized, "A valid X-Api-Key header is required");
		return;
	}

	await next(context);
});

app.MapOpenApi();
app.MapScalarApiReference();

app.MapHomeEndpoints();
app.MapDeviceEndpoints();
app.MapAutomationEndpoints();
app.MapSensorEndpoints();
app.MapSettingEndpoints();
app.MapMachineEndpoints();
app.MapServiceEndpoints();
app.MapScheduleEndpoints();
app.MapAiEndpoints();
app.MapHistoryEndpoints();
app.MapNotificationEndpoints();

// Build the route graph now to catch bad endpoints
try
{
	foreach (EndpointDataSource source in ((IEndpointRouteBuilder)app).DataSources)
		_ = source.Endpoints;
}
catch (Exception ex)
{
	Log.Error("Api", $"The route graph is invalid, refusing to start: {ex.Message}");
	throw;
}

app.Services.GetRequiredService<StateBroadcaster>();
app.Services.GetRequiredService<SettingsService>();
app.Services.GetRequiredService<AiService>();

Log.Write("Kernel", "Online");
await app.RunAsync();
Log.Write("Kernel", "Stopped");
return;

static async Task Deny(HttpContext context, int status, string detail)
{
	context.Response.StatusCode = status;

	if (context.Request.Headers.Accept.Contains("text/plain"))
	{
		context.Response.ContentType = "text/plain";
		await context.Response.WriteAsync(detail);
		return;
	}

	context.Response.ContentType = "application/problem+json";
	await context.Response.WriteAsJsonAsync(
		new ProblemResponse(status == StatusCodes.Status401Unauthorized ? "Unauthorized" : "Unavailable", status, detail, context.Request.Path),
		CortanaEnvironment.WireJson);
}
