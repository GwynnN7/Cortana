using System.Globalization;
using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.API.Endpoints;

public static class AiEndpoints
{
	public static void MapAiEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup($"/{ERoute.AI}").WithTags("AI");

		group.MapPost("", Ask)
			.Access(EApiAccess.Sensitive)
			.WithName("Chat")
			.WithSummary("Sends a message to the LLM and returns Cortana's reply. Set remember=false for a one-shot message with no history.")
			.Produces<ChatResponse>();

		group.MapDelete("/{conversation}", Reset)
			.Access(EApiAccess.Sensitive)
			.WithName("ResetConversation")
			.WithSummary("Forgets the history of one conversation.")
			.Produces<MessageResponse>();

		group.MapGet("/prompt", Prompt)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetSystemPrompt")
			.WithSummary("The system prompt currently in use.")
			.Produces<MessageResponse>();

		group.MapPost("/prompt", SetPrompt)
			.Access(EApiAccess.Sensitive)
			.WithName("SetSystemPrompt")
			.WithSummary("Replaces the system prompt. Takes effect on the next message.")
			.Produces<MessageResponse>();

		group.MapDelete("/prompt", ResetPrompt)
			.Access(EApiAccess.Sensitive)
			.WithName("ResetSystemPrompt")
			.WithSummary("Restores the system prompt that ships with Cortana.")
			.Produces<MessageResponse>();

		group.MapGet("/models", Models)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetModels")
			.WithSummary("Every language model that can be selected, and which one is active.")
			.Produces<ModelListResponse>();

		group.MapGet("/model", CurrentModel)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetModel")
			.WithSummary("The language model currently in use.")
			.Produces<ModelResponse>();

		group.MapGet("/settings", AllSettings)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetAiSettings")
			.WithSummary("Temperature, history depth and the Discord session length.")
			.Produces<AiSettingsListResponse>();

		group.MapGet("/settings/{setting}", GetSetting)
			.Access(EApiAccess.ReadOnly)
			.WithName("GetAiSetting")
			.WithSummary("One AI setting.")
			.Produces<AiSettingResponse>();

		group.MapPost("/settings/{setting}", SetSetting)
			.Access(EApiAccess.Sensitive)
			.WithName("SetAiSetting")
			.WithSummary("Updates one AI setting.")
			.Produces<AiSettingResponse>();

		group.MapPost("/model", SetModel)
			.Access(EApiAccess.Sensitive)
			.WithName("SetModel")
			.WithSummary("Switches the language model.")
			.Produces<ModelResponse>();
	}

	private static IResult Models(HttpRequest request)
	{
		IReadOnlyList<ModelResponse> models = LlmService.Models();
		string text = string.Join("\n", models.Select(model => $"{(model.Current ? "*" : " ")} {model.Name}"));

		return ApiResults.Ok(request, text, new ModelListResponse(models, LlmService.ModelName));
	}

	private static IResult CurrentModel(HttpRequest request)
	{
		ModelResponse current = LlmService.Models().First(model => model.Current);
		return ApiResults.Ok(request, $"{current.Name} ({current.Id})", current);
	}

	private static IResult SetModel(PostModel body, HttpRequest request)
	{
		if (!LlmService.TryParseModel(body.Model, out ELlmModel parsed))
			return ApiResults.NotFound(request, $"Model '{body.Model}' not found. Valid values: {string.Join(", ", LlmService.Models().Select(model => model.Name))}");

		return ApiResults.From(request, LlmService.SetModel(parsed), message =>
		{
			ModelResponse current = LlmService.Models().First(model => model.Current);
			return (message, current);
		});
	}

	private static IResult SetPrompt(PostPrompt body, HttpRequest request) =>
		ApiResults.From(request, LlmService.SetPrompt(body.Prompt));

	private static IResult ResetPrompt(HttpRequest request) =>
		ApiResults.From(request, LlmService.ResetPrompt());

	private static IResult AllSettings(HttpRequest request)
	{
		IReadOnlyList<AiSettingResponse> settings = AiSettings.All();
		string text = string.Join("\n", settings.Select(setting => $"{setting.Setting}: {setting.Value}"));

		return ApiResults.Ok(request, text, new AiSettingsListResponse(settings));
	}

	private static IResult GetSetting(string setting, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out EAiSetting parsed)) return ApiResults.UnknownValue<EAiSetting>(request, "Setting", setting);

		return ApiResults.Ok(request, AiSettings.Read(parsed), new AiSettingResponse(parsed.ToString(), AiSettings.Read(parsed)));
	}

	private static IResult SetSetting(string setting, PostNumber body, HttpRequest request)
	{
		if (!ApiResults.TryParseEnum(setting, out EAiSetting parsed)) return ApiResults.UnknownValue<EAiSetting>(request, "Setting", setting);

		return ApiResults.From(request, AiSettings.Write(parsed, body.Value),
			message => (message, new AiSettingResponse(parsed.ToString(), AiSettings.Read(parsed))));
	}

	private static async Task<IResult> Ask(PostChat body, HttpRequest request)
	{
		if (!LlmService.IsConfigured) return ApiResults.Unavailable(request, "CORTANA_GEMINI_KEY is not configured");

		StringResult result = await LlmService.Ask(body.Conversation, body.Message, body.Author, body.Remember, body.Owner);
		return ApiResults.From(request, result, reply => (reply, new ChatResponse(reply, body.Conversation)));
	}

	private static IResult Reset(string conversation, HttpRequest request)
	{
		LlmService.ResetConversation(conversation);
		return ApiResults.Message(request, $"Conversation '{conversation}' reset");
	}

	private static IResult Prompt(HttpRequest request) => ApiResults.Message(request, LlmService.SystemPrompt);
}
