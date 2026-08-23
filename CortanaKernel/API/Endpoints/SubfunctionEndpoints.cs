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

	private static IResult PublishMessage(PostCommand message, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(message.Command, out EMessageCategory category))
			return ApiResults.UnknownValue<EMessageCategory>(request, "Category", message.Command);

		IpcHandler.Publish(category, string.IsNullOrEmpty(message.Args) ? "Hi, I'm Cortana" : message.Args);
		return ApiResults.Message(request, "Message published!");
	}
}
