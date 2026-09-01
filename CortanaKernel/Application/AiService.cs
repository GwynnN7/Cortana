using System.Collections.Concurrent;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// The conversational part. `chat` keeps a persistent conversation while `ask` does not
public sealed class AiService(
	IAiProvider provider,
	IConversationRepository conversations,
	CapabilityRegistry capabilities,
	AiSettingsStore settings,
	NotificationService notifications,
	IEventBus bus)
{
	private static readonly string PromptPath = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaKernel/Prompt.txt");
	private static readonly string ShippedPrompt = Path.Combine(CortanaEnvironment.Path_(CortanaFolder.Storage), "prompt.txt");

	private const string OwnerNote = "- You are talking to gwynn7 now.";
	private const string ReadOnlyNote =
		"- In this conversation your tools are read-only: you can look at the house and the computer but not change them. If someone asks you to switch something, say you cannot from here.";

	private readonly ConcurrentDictionary<string, SemaphoreSlim> _turnLocks = new();

	public bool IsConfigured => provider.IsConfigured;

	public string ModelName => provider.ModelName;

	public IReadOnlyList<ModelView> Models() => provider.Models();

	public Result<string> SelectModel(string model) => provider.SelectModel(model);

	public IReadOnlyList<AiSettingView> Settings() => settings.All();

	public string ReadSetting(AiSettingKey key) => settings.Read(key);

	public Result<string> WriteSetting(AiSettingKey key, double value) => settings.Write(key, value);

	public string SystemPrompt
	{
		get
		{
			if (File.Exists(PromptPath)) return File.ReadAllText(PromptPath);
			return File.Exists(ShippedPrompt) ? File.ReadAllText(ShippedPrompt) : "You are Cortana, a concise and witty home assistant.";
		}
	}

	public Result<string> SetPrompt(string prompt)
	{
		if (string.IsNullOrWhiteSpace(prompt)) return Result.Fail<string>("The prompt cannot be empty");

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PromptPath)!);
			File.WriteAllText(PromptPath, prompt.Trim());
			return Result.Ok("Prompt saved");
		}
		catch (Exception ex)
		{
			return Result.Fail<string>($"Could not save the prompt: {ex.Message}");
		}
	}

	public Result<string> ResetPrompt()
	{
		try
		{
			if (File.Exists(PromptPath)) File.Delete(PromptPath);
			return Result.Ok("Prompt restored to the one that ships with Cortana");
		}
		catch (Exception ex)
		{
			return Result.Fail<string>($"Could not restore the prompt: {ex.Message}");
		}
	}

	public void Forget(string conversation)
	{
		conversations.Delete(conversation);
		bus.Publish(new ConversationUpdated(conversation, DateTimeOffset.Now));
	}

	public string Mood { get; set; } = "";

	public string Activity { get; set; } = "";

	public async Task<Result<string>> Ask(AskRequest request, CommandOrigin origin, CancellationToken token = default)
	{
		if (!provider.IsConfigured) return Result.Fail<string>("No language model is configured");
		if (string.IsNullOrWhiteSpace(request.Message)) return Result.Fail<string>("Nothing to answer");

		// Turns inside one conversation are sequential, separate conversations run independently
		SemaphoreSlim gate = _turnLocks.GetOrAdd(request.Conversation, _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(token);

		try
		{
			return await Exchange(request, origin, token);
		}
		catch (Exception ex)
		{
			Log.Error("Ai", ex.Message);
			return Result.Fail<string>("I could not reach my language model");
		}
		finally
		{
			gate.Release();
		}
	}

	private async Task<Result<string>> Exchange(AskRequest request, CommandOrigin origin, CancellationToken token)
	{
		Conversation? conversation = request.Remember ? conversations.Load(request.Conversation) : null;
		IReadOnlyCollection<AiCapability> tools = capabilities.For(request.Trusted);

		string mood = string.IsNullOrEmpty(Mood) ? "" : $"\n- Current mood: {Mood}.";
		if (!string.IsNullOrEmpty(Activity)) mood += $"\n- {Activity}";

		string instructions = SystemPrompt + mood + "\n" + (request.Trusted
			? OwnerNote
			: $"- You are talking to a guest ({request.Author}), not gwynn7. Stay friendly and helpful, but keep it in mind.");

		if (!request.Trusted) instructions += "\n" + ReadOnlyNote;

		string message = string.IsNullOrWhiteSpace(request.Author) ? request.Message : $"{request.Author}: {request.Message}";

		var aiRequest = new AiRequest(
			instructions,
			conversation?.Messages ?? [],
			message,
			[.. tools.Select(capability => capability.Descriptor)],
			settings.Number(AiSettingKey.Temperature));

		Result<string> reply = await provider.Complete(aiRequest, call => Invoke(call, request.Trusted, origin, token), token);
		if (!reply.IsOk) return reply;

		if (!request.Remember) return reply;

		List<ChatMessage> messages =
		[
			.. conversation?.Messages ?? [],
			new ChatMessage(ChatRole.User, message, DateTimeOffset.Now),
			new ChatMessage(ChatRole.Assistant, reply.Value, DateTimeOffset.Now)
		];

		int keep = settings.Integer(AiSettingKey.RememberedExchanges) * 2;
		if (messages.Count > keep) messages = [.. messages.Skip(messages.Count - keep)];

		conversations.Save(new Conversation(request.Conversation, messages, DateTimeOffset.Now));
		bus.Publish(new ConversationUpdated(request.Conversation, DateTimeOffset.Now));

		return reply;
	}

	/// The single door between the model and Cortana
	private async Task<string> Invoke(AiToolCall call, bool trusted, CommandOrigin origin, CancellationToken token)
	{
		AiCapability? capability = capabilities.Find(call.Name);
		if (capability == null) return $"Unknown tool '{call.Name}'";
		if (!trusted && !capability.IsReadOnly) return $"'{call.Name}' is not available in this conversation";

		try
		{
			// A user asking through the AI is still the user
			return await capability.Execute(call.Arguments, origin with { ViaAi = true }, token);
		}
		catch (Exception ex)
		{
			Log.Error("Ai", $"{call.Name} failed: {ex.Message}");
			notifications.Raise(NotificationSource.Ai, $"The tool {call.Name} failed: {ex.Message}", NotificationLevel.Warning);
			return $"'{call.Name}' failed: {ex.Message}";
		}
	}
}
