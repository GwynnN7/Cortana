using CortanaLib;
using CortanaLib.Structures;
using CortanaWeb.Components;
using CortanaWeb.Services;

namespace CortanaWeb;

public static class CortanaWebApp
{
	public static async Task Main(string[] args)
	{
		DataHandler.LoadEnvironment(required: false);

		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.Services.AddLogging(c => c.ClearProviders().AddSimpleConsole(options => options.SingleLine = true));

		builder.Services.AddRazorComponents().AddInteractiveServerComponents();
		builder.Services.AddCascadingAuthenticationState();

		var auth = new WebAuth();
		builder.Services.AddSingleton(auth);

		builder.Services.AddSingleton<CortanaState>();
		builder.Services.AddHostedService(sp => sp.GetRequiredService<CortanaState>());

		builder.Services.AddAuthentication(WebAuth.CookieScheme)
			.AddCookie(WebAuth.CookieScheme, options =>
			{
				options.LoginPath = WebAuth.LoginPage;
				options.ExpireTimeSpan = TimeSpan.FromDays(30);
				options.SlidingExpiration = true;
			});

		builder.Services.AddAuthorizationBuilder()
			.AddPolicy(WebAuth.Policy, policy =>
				policy.RequireAssertion(context => !auth.Enabled || context.User.Identity?.IsAuthenticated == true));

		WebApplication app = builder.Build();

		app.Urls.Add($"http://*:{DataHandler.EnvOrNull("CORTANA_WEB_PORT") ?? "5118"}");

		app.UseAntiforgery();
		app.UseAuthentication();
		app.UseAuthorization();
		app.MapStaticAssets();

		MapAuthEndpoints(app);
		MapMediaEndpoints(app);

		app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

		if (!auth.Enabled) DataHandler.Log("[Web] CORTANA_WEB_PASSWORD not set - the dashboard is unauthenticated.");

		await app.RunAsync();
	}

	private static void MapAuthEndpoints(WebApplication app)
	{
		app.MapPost("/auth/login", async (HttpContext context, WebAuth auth) =>
		{
			IFormCollection form = await context.Request.ReadFormAsync();
			string target = form["returnUrl"].ToString();

			if (!auth.Validate(form["password"]))
				return Results.Redirect($"{WebAuth.LoginPage}?error=1&returnUrl={Uri.EscapeDataString(SafeReturnUrl(target))}");

			await WebAuth.SignIn(context);
			return Results.Redirect(SafeReturnUrl(target));
		}).DisableAntiforgery();

		app.MapPost("/auth/logout", async (HttpContext context) =>
		{
			await WebAuth.SignOut(context);
			return Results.Redirect(WebAuth.LoginPage);
		}).DisableAntiforgery();
	}

		private static string SafeReturnUrl(string? target) =>
		!string.IsNullOrEmpty(target) && target.StartsWith('/') && !target.StartsWith("//") ? target : "/";

	private static void MapMediaEndpoints(WebApplication app)
	{
		RouteGroupBuilder media = app.MapGroup("/media").RequireAuthorization(WebAuth.Policy);

		media.MapGet("/qr", (string content, bool border = true, bool classic = false) =>
		{
			if (string.IsNullOrWhiteSpace(content)) return Results.BadRequest("Missing content");
			Stream image = MediaHandler.CreateQrCode(content, classic, border);
			return Results.Stream(image, "image/png", "qrcode.png");
		});

		media.MapGet("/audio", async (string url) =>
		{
			AudioTrack? track = await MediaHandler.GetAudioTrack(url);
			if (track == null) return Results.NotFound("Video not available");

			Stream audio = await MediaHandler.GetAudioStream(track.OriginalUrl);
			return Results.Stream(audio, "audio/mpeg", $"{Sanitize(track.Title)}.mp3");
		});

		media.MapGet("/video", async (string url, EVideoQuality quality = EVideoQuality.Balanced, int maxSize = 200) =>
		{
			AudioTrack? track = await MediaHandler.GetAudioTrack(url);
			if (track == null) return Results.NotFound("Video not available");

			string folder = DataHandler.CortanaPath(EDirType.Temp);
			await MediaHandler.DownloadVideo(track.OriginalUrl, quality, maxSize, folder);

			string path = Path.Combine(folder, "temp_video.mp4");
			if (!File.Exists(path)) return Results.Problem("Download failed");

			var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
			return Results.Stream(stream, "video/mp4", $"{Sanitize(track.Title)}.mp4");
		});
	}

	private static string Sanitize(string name) =>
		string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
