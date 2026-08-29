using CortanaKernel.Hardware;
using CortanaKernel.Kernel;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class SubfunctionEndpoints
{
	public static void MapSubfunctionEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.SubFunctions}").WithTags("Subfunctions");

		group.MapGet("", AllStatuses)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSubfunctions")
			.WithSummary("Running state of every subfunction.")
			.Produces<SubfunctionListResponse>();

		group.MapGet("/logs", AllLogSettings)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetLogSettings")
			.WithSummary("Where Cortana mirrors her log lines.")
			.Produces<SettingsListResponse>();

		group.MapPost("/logs/{target}", SetLogSetting)
			.Access(EApiAccess.Sensitive)
			.WithName("SetLogSetting")
			.WithSummary("Turns logging to one destination on or off. Any other number toggles.")
			.Produces<SettingsResponse>();

		group.MapGet("/{subfunction}/journal", Journal)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSubfunctionJournal")
			.WithSummary("Recent systemd journal lines for one subfunction.")
			.Produces<MessageResponse>();

		group.MapGet("/{subfunction}", Status)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSubfunction")
			.WithSummary("Running state of one subfunction.")
			.Produces<SubfunctionResponse>();

		group.MapPost("/{subfunction}", Handle)
			.Access(EApiAccess.Sensitive)
			.WithName("ControlSubfunction")
			.WithSummary("Starts, stops, restarts or updates a subfunction.")
			.Produces<MessageResponse>();

		group.MapPost("", PublishMessage)
			.Access(EApiAccess.Sensitive)
			.WithName("PublishMessage")
			.WithSummary("Publishes a message to a bot through Redis IPC.")
			.Produces<MessageResponse>();
	}

	private static async Task<IResult> AllStatuses(HttpRequest request)
	{
		IReadOnlyList<SubfunctionResponse> statuses = await Bootloader.GetAllStatuses();
		string text = string.Join("\n", statuses.Select(s => $"{s.Subfunction} is {(s.Running ? "running" : "not running")}"));
		return ApiResults.Ok(request, text, new SubfunctionListResponse(statuses));
	}

	private static async Task<IResult> Status(string subfunction, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(subfunction, out ESubFunctionType parsed))
			return ApiResults.UnknownValue<ESubFunctionType>(request, "Subfunction", subfunction);

		bool running = await Bootloader.IsSubfunctionRunning(parsed);
		return ApiResults.Ok(request,
			running ? $"{parsed} is running!" : $"{parsed} is not running.",
			new SubfunctionResponse(parsed.ToString(), running));
	}

	private static async Task<IResult> Handle(string subfunction, PostAction command, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(subfunction, out ESubFunctionType parsed))
			return ApiResults.UnknownValue<ESubFunctionType>(request, "Subfunction", subfunction);
		if (!ApiResults.TryParseEnum(command.Action, out ESubfunctionAction action))
			return ApiResults.UnknownValue<ESubfunctionAction>(request, "Action", command.Action);

		return ApiResults.From(request, await Bootloader.SubfunctionCall(parsed, action));
	}

	private static async Task<IResult> Journal(string subfunction, int? lines, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(subfunction, out ESubFunctionType parsed))
			return ApiResults.UnknownValue<ESubFunctionType>(request, "Subfunction", subfunction);

		return ApiResults.From(request, await Bootloader.Journal(parsed, lines ?? 100));
	}

	private static bool TryParseTarget(string target, out ESettings setting)
	{
		if (ApiResults.TryParseEnum(target, out setting) && setting.IsLog()) return true;

		return ApiResults.TryParseEnum($"LogTo{target}", out setting) && setting.IsLog();
	}

	private static IResult AllLogSettings(HttpRequest request)
	{
		IReadOnlyList<SettingsResponse> settings = HardwareApi.Sensors.GetAllSettings()
			.Where(setting => Enum.Parse<ESettings>(setting.Setting).IsLog()).ToList();

		string text = string.Join("\n", settings.Select(setting => $"{setting.Setting.Replace("LogTo", "")}: {setting.Value}"));
		return ApiResults.Ok(request, text, new SettingsListResponse(settings));
	}

	private static IResult SetLogSetting(string target, PostValue value, HttpRequest request)
	{
		if (!TryParseTarget(target, out ESettings parsed))
			return ApiResults.NotFound(request, $"Log target '{target}' not found. Valid values: Web, Telegram, Discord");

		return ApiResults.From(request, HardwareApi.Sensors.SetSettings(parsed, value.Value),
			updated => ($"{parsed}: {updated}", new SettingsResponse(parsed.ToString(), updated)));
	}

	private static IResult PublishMessage(PostCommand message, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(message.Command, out EMessageCategory category))
			return ApiResults.UnknownValue<EMessageCategory>(request, "Category", message.Command);

		IpcHandler.Publish(category, string.IsNullOrEmpty(message.Args) ? "Hi, I'm Cortana" : message.Args);
		return ApiResults.Message(request, "Message published!");
	}
}
