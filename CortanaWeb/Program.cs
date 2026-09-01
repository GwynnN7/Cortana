using CortanaLib.Media;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using CortanaWeb.Components;
using CortanaWeb.Services;

CortanaEnvironment.Load(required: false);

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
	Args = args,
	ContentRootPath = AppContext.BaseDirectory
});

builder.Services.AddLogging(logging => logging.ClearProviders().AddSimpleConsole(options => options.SingleLine = true));
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();

var auth = new WebAuth();
builder.Services.AddSingleton(auth);

builder.Services.AddSingleton<CortanaState>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<CortanaState>());

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
app.Urls.Add($"http://*:{CortanaEnvironment.Read("CORTANA_WEB_PORT", "5118")}");

app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

app.MapPost("/auth/login", async (HttpContext context, WebAuth webAuth) =>
{
	IFormCollection form = await context.Request.ReadFormAsync();
	string target = form["returnUrl"].ToString();

	if (!webAuth.Validate(form["password"]))
		return Results.Redirect($"{WebAuth.LoginPage}?error=1&returnUrl={Uri.EscapeDataString(SafeReturn(target))}");

	await WebAuth.SignIn(context);
	return Results.Redirect(SafeReturn(target));
}).DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext context) =>
{
	await WebAuth.SignOut(context);
	return Results.Redirect(WebAuth.LoginPage);
}).DisableAntiforgery();

RouteGroupBuilder media = app.MapGroup("/media").RequireAuthorization(WebAuth.Policy);

media.MapGet("/qr", (string content, bool border = true, bool classic = false) =>
{
	if (string.IsNullOrWhiteSpace(content)) return Results.BadRequest("Missing content");
	return Results.Stream(MediaLibrary.CreateQrCode(content, classic, border), "image/png", "qrcode.png");
});

media.MapGet("/audio", async (string url) =>
{
	Result<AudioTrack> track = await MediaLibrary.ResolveTrack(url);
	if (!track.IsOk) return Results.NotFound(track.Error);

	Result<Stream> audio = await MediaLibrary.OpenAudioStream(track.Value.OriginalUrl);
	return audio.IsOk
		? Results.Stream(audio.Value, "audio/mpeg", $"{Sanitize(track.Value.Title)}.mp3")
		: Results.Problem(audio.Error);
});

media.MapGet("/video", async (string url, VideoQuality quality = VideoQuality.Balanced, int maxSize = 200) =>
{
	Result<AudioTrack> track = await MediaLibrary.ResolveTrack(url);
	if (!track.IsOk) return Results.NotFound(track.Error);

	string folder = CortanaEnvironment.Path_(CortanaFolder.Temp);
	Result<string> file = await MediaLibrary.DownloadVideo(track.Value.OriginalUrl, quality, maxSize, folder);
	if (!file.IsOk) return Results.Problem(file.Error);

	var stream = new FileStream(file.Value, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);
	return Results.Stream(stream, "video/mp4", $"{Sanitize(track.Value.Title)}.mp4");
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

if (!auth.Enabled) Log.Write("Web", "CORTANA_WEB_PASSWORD is not set, so the dashboard is unauthenticated");
if (!MediaLibrary.YoutubeAvailable) Log.Write("Web", "yt-dlp is not installed, so the YouTube tools will fail");

await app.RunAsync();
return;

static string SafeReturn(string? target) =>
	!string.IsNullOrEmpty(target) && target.StartsWith('/') && !target.StartsWith("//") ? target : "/";

static string Sanitize(string name) => string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
