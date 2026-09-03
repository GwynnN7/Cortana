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
			.WithSummary("Sends a message to Cortana. Set remember=false for a one-shot ask that is not stored in the conversation")
			.Produces<AskResponse>();

		group.MapGet("/{conversation}", (string conversation, AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<ChatTurn> turns = ai.History(conversation);
				string text = turns.Count == 0
					? $"Nothing said in '{conversation}' yet"
					: string.Join("\n", turns.Select(turn => $"{turn.At:HH:mm} {(turn.Mine ? "you" : "Cortana")}: {turn.Text}"));

				return ApiResults.Ok(request, text, new ConversationResponse(conversation, turns));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Everything said in one conversation").Produces<ConversationResponse>();

		group.MapDelete("/{conversation}", (string conversation, AiService ai, HttpRequest request) =>
			{
				ai.Forget(conversation);
				return ApiResults.Message(request, $"Conversation '{conversation}' forgotten");
			})
			.Access(ApiAccess.Sensitive).WithSummary("Forgets one conversation");

		group.MapGet("/prompt", (AiService ai, HttpRequest request) => ApiResults.Message(request, ai.SystemPrompt))
			.Access(ApiAccess.ReadOnly).WithSummary("The system prompt currently in use");

		group.MapGet("/memory", (AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<MemoryEntry> memories = ai.Memories();
				string text = memories.Count == 0
					? "I have not been told anything about you yet"
					: string.Join("\n", memories.Select(memory => $"[{memory.Id}] ({memory.Kind}) {memory.Text}"));

				return ApiResults.Ok(request, text, new MemoryListResponse(memories));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Everything Cortana remembers about the owner").Produces<MemoryListResponse>();

		group.MapPost("/memory", (RememberRequest body, AiService ai, HttpRequest request) =>
				ApiResults.From(request, ai.Remember(body.Text, body.Kind, body.Source), memory => $"Remembered: {memory.Text}"))
			.Access(ApiAccess.Sensitive).WithSummary("Tell Cortana something worth keeping");

		group.MapDelete("/memory/{id}", (string id, AiService ai, HttpRequest request) =>
				ApiResults.From(request, ai.ForgetMemory(id)))
			.Access(ApiAccess.Sensitive).WithSummary("Make Cortana forget one thing");

		group.MapGet("/quiet", (VolitionService volition, HttpRequest request) =>
				ApiResults.Message(request, volition.State.QuietUntil is { } until && until > DateTimeOffset.Now
					? $"Quiet until {until:HH:mm}"
					: "Not quiet"))
			.Access(ApiAccess.ReadOnly).WithSummary("Whether Cortana is holding her tongue, and until when");

		group.MapPost("/quiet", (QuietRequest body, VolitionService volition, HttpRequest request) =>
				ApiResults.From(request, volition.Quiet(body.Minutes)))
			.Access(ApiAccess.Sensitive).WithSummary("Stop Cortana speaking unprompted for a while");

		group.MapDelete("/quiet", (VolitionService volition, HttpRequest request) =>
				ApiResults.From(request, volition.Speak()))
			.Access(ApiAccess.Sensitive).WithSummary("Let Cortana speak unprompted again");

		group.MapPost("/prompt", (PromptRequest body, AiService ai, HttpRequest request) => ApiResults.From(request, ai.SetPrompt(body.Prompt)))
			.Access(ApiAccess.Sensitive).WithSummary("Replaces the system prompt");

		group.MapDelete("/prompt", (AiService ai, HttpRequest request) => ApiResults.From(request, ai.ResetPrompt()))
			.Access(ApiAccess.Sensitive).WithSummary("Restores the system prompt that ships with Cortana");

		group.MapGet("/models", (AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<ModelView> models = ai.Models();
				string text = string.Join("\n", models.Select(model => $"{(model.Current ? "*" : " ")} {model.Name}"));
				return ApiResults.Ok(request, text, new ModelListResponse(models, ai.ModelName));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Every selectable language model").Produces<ModelListResponse>();

		group.MapPost("/model", (ModelRequest body, AiService ai, HttpRequest request) => ApiResults.From(request, ai.SelectModel(body.Model)))
			.Access(ApiAccess.Sensitive).WithSummary("Switches the language model");

		group.MapGet("/settings", (AiService ai, HttpRequest request) =>
			{
				IReadOnlyList<AiSettingView> settings = ai.Settings();
				return ApiResults.Ok(request, string.Join("\n", settings.Select(view => $"{view.Setting}: {view.Value}")),
					new AiSettingListResponse(settings));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Model behaviour, memory depth and telemetry cadence")
			.Produces<AiSettingListResponse>();

		group.MapGet("/settings/{setting}", (string setting, AiService ai, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out AiSettingKey parsed)) return ApiResults.Unknown<AiSettingKey>(request, "Setting", setting);

				return ApiResults.Ok(request, ai.ReadSetting(parsed), new AiSettingView(parsed, ai.ReadSetting(parsed)));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("One AI setting").Produces<AiSettingView>();

		group.MapPost("/settings/{setting}", (string setting, NumberRequest body, AiService ai, HttpRequest request) =>
			{
				if (!ApiResults.TryParse(setting, out AiSettingKey parsed)) return ApiResults.Unknown<AiSettingKey>(request, "Setting", setting);

				return ApiResults.From(request, ai.WriteSetting(parsed, body.Value),
					message => (message, new AiSettingView(parsed, ai.ReadSetting(parsed))));
			})
			.Access(ApiAccess.Sensitive).WithSummary("Updates one AI setting").Produces<AiSettingView>();
	}
}
