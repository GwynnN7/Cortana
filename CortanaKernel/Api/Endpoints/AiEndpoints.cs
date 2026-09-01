using CortanaKernel.Application;
using CortanaLib.Contracts;

namespace CortanaKernel.Api.Endpoints;

public static class AiEndpoints
{
	public static void MapAiEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/ai").WithTags("AI");

		group.MapPost("", async (AskRequest body, AiService ai, HttpRequest request, CancellationToken token) =>
			{
				if (!ai.IsConfigured) return ApiResults.Unavailable(request, "No language model is configured");

				return ApiResults.From(request, await ai.Ask(body, RequestOrigin.From(request), token),
					reply => (reply, new AskResponse(reply, body.Conversation)));
			})
			.Access(ApiAccess.Sensitive)
			.WithSummary("Sends a message to Cortana. Set remember=false for a one-shot ask that is not stored in the conversation.")
			.Produces<AskResponse>();

		group.MapDelete("/{conversation}", (string conversation, AiService ai, HttpRequest request) =>
			{
				ai.Forget(conversation);
				return ApiResults.Message(request, $"Conversation '{conversation}' forgotten");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Forgets one conversation.");

		group.MapGet("/prompt", (AiService ai, HttpRequest request) => ApiResults.Message(request, ai.SystemPrompt))
			.Access(ApiAccess.ReadOnly).WithSummary("The system prompt currently in use.");

		group.MapPost("/prompt", (PromptRequest body, AiService ai, HttpRequest request) => ApiResults.From(request, ai.SetPrompt(body.Prompt)))
			.Access(ApiAccess.Sensitive).WithSummary("Replaces the system prompt.");

		group.MapDelete("/prompt", (AiService ai, HttpRequest request) => ApiResults.From(request, ai.ResetPrompt()))
			.Access(ApiAccess.Sensitive).WithSummary("Restores the system prompt that ships with Cortana.");

		group.MapGet("/models", (AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<ModelView> models = ai.Models();
				string text = string.Join("\n", models.Select(model => $"{(model.Current ? "*" : " ")} {model.Name}"));
				return ApiResults.Ok(request, text, new ModelListResponse(models, ai.ModelName));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every selectable language model.").Produces<ModelListResponse>();

		group.MapPost("/model", (ModelRequest body, AiService ai, HttpRequest request) => ApiResults.From(request, ai.SelectModel(body.Model)))
			.Access(ApiAccess.Sensitive).WithSummary("Switches the language model.");

		group.MapGet("/settings", (AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<AiSettingView> settings = ai.Settings();
				return ApiResults.Ok(request, string.Join("\n", settings.Select(view => $"{view.Setting}: {view.Value}")),
					new AiSettingListResponse(settings));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Model behaviour, memory depth and telemetry cadence.")
			.Produces<AiSettingListResponse>();

		group.MapGet("/settings/{setting}", (string setting, AiService ai, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out AiSettingKey parsed)) return ApiResults.Unknown<AiSettingKey>(request, "Setting", setting);

				return ApiResults.Ok(request, ai.ReadSetting(parsed), new AiSettingView(parsed, ai.ReadSetting(parsed)));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One AI setting.").Produces<AiSettingView>();

		group.MapPost("/settings/{setting}", (string setting, NumberRequest body, AiService ai, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out AiSettingKey parsed)) return ApiResults.Unknown<AiSettingKey>(request, "Setting", setting);

				return ApiResults.From(request, ai.WriteSetting(parsed, body.Value),
					message => (message, new AiSettingView(parsed, ai.ReadSetting(parsed))));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Updates one AI setting.").Produces<AiSettingView>();
	}
}
