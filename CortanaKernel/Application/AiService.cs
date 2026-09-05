using CortanaKernel.Domain.Settings;
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
	SettingsStore flags,
	MemoryStore memories,
	Lazy<SnapshotService> snapshots,
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

	public IReadOnlyList<ChatTurn> History(string conversation) =>
	[
		.. (conversations.Load(conversation)?.Messages ?? [])
			.Select(message => new ChatTurn(message.Role == ChatRole.User, Spoken(message), message.At))
	];

	/// The author prefix belongs to the model, not to whoever reads the conversation back
	private static string Spoken(ChatMessage message) =>
		message.Author.Length > 0 && message.Text.StartsWith($"{message.Author}: ", StringComparison.Ordinal)
			? message.Text[(message.Author.Length + 2)..]
			: message.Text;

	/// Cortana speaking first: the turn is stored so the dashboard shows it like any other
	public void Append(string conversation, string text)
	{
		Conversation? current = conversations.Load(conversation);
		DateTimeOffset now = DateTimeOffset.Now;

		conversations.Save(new Conversation(conversation,
			[.. current?.Messages ?? [], new ChatMessage(ChatRole.Assistant, text, now)], now));

		bus.Publish(new ConversationUpdated(conversation, now));
	}

	public void Forget(string conversation)
	{
		conversations.Delete(conversation);
		bus.Publish(new ConversationUpdated(conversation, DateTimeOffset.Now));
	}


	public IReadOnlyList<MemoryEntry> Memories() => memories.All();

	public Result<MemoryEntry> Remember(string text, MemoryKind kind, string source) =>
		memories.Remember(text, kind, source, StateLifetime);

	/// The longest a state may be asked to last. Beyond a month it is not where he is, it is who he is
	private const double LongestState = 720;

	public TimeSpan StateLifetime => StateHorizon(null);

	/// How long a state should hold. She is the one who heard whether he said "back in an hour" or
	/// "away for a few days", so she is allowed to say; the setting is only what it falls back to
	public TimeSpan StateHorizon(int? hours) => TimeSpan.FromHours(hours is { } wanted
		? Math.Clamp(wanted, 1, LongestState)
		: Math.Clamp(settings.Number(AiSettingKey.MemoryStateHours), 1, LongestState));

	public Result<string> ForgetMemory(string id) => memories.Forget(id);

	/// What she currently believes about where he is, put in front of her every turn so a belief that
	/// has gone stale, or was never written down, is obvious while there is still someone to ask
	private string Standing() =>
		memories.All().FirstOrDefault(memory => memory.Kind == MemoryKind.State)?.Text
		?? "nothing written down, so presumably going about his day here";

	private string Recollection(string message)
	{
		if (!flags.Flag(SettingKey.MemoryEnabled)) return "";

		var depth = (int)settings.Number(AiSettingKey.MemoryDepth);
		if (depth <= 0) return "";

		IReadOnlyList<MemoryEntry> recalled = memories.Recall(message, depth);
		return string.Join("\n", recalled.Select(memory => $"- ({memory.Kind.ToString().ToLowerInvariant()}) {memory.Text}"));
	}

	/// The morning greeting and the wrap-up are two short sentences that nobody is waiting on, so they
	/// are never worth a request from a family that gets five a day. They take the cheap one, and when
	/// even that is spent Compose falls back to a plain line rather than borrowing from the good model
	private const LlmFamily Narration = LlmFamily.FlashLite;

	/// Phrase something in her own voice, falling back to a plain line when no model can answer.
	/// She is writing a line here, not running the house, so she is handed no tools: given them she
	/// uses them, and the wrap-up that should have summarised the day called Remember and then stored
	/// its own "I've saved that note" as the summary
	public async Task<string> Compose(string brief, string fallback, int limit = 240, CancellationToken token = default)
	{
		if (!provider.IsConfigured) return fallback;

		try
		{
			Result<string> reply = await Ask(new AskRequest(brief, "cortana", "gwynn7", Remember: false),
				CommandOrigin.Internal, tools: false, Narration, token);
			if (!reply.IsOk) return fallback;

			string spoken = reply.Value.ReplaceLineEndings(" ").Trim();
			if (spoken.Length == 0) return fallback;

			return spoken.Length <= limit ? spoken : string.Concat(spoken.AsSpan(0, limit - 1), "…");
		}
		catch (Exception ex)
		{
			Log.Error("Ai", $"Could not compose a line: {ex.Message}");
			return fallback;
		}
	}

	public Task<Result<string>> Ask(AskRequest request, CommandOrigin origin, CancellationToken token = default) =>
		Ask(request, origin, tools: true, model: null, token);

	private async Task<Result<string>> Ask(AskRequest request, CommandOrigin origin, bool tools, LlmFamily? model, CancellationToken token)
	{
		if (!provider.IsConfigured) return Result.Fail<string>("No language model is configured");
		if (string.IsNullOrWhiteSpace(request.Message)) return Result.Fail<string>("Nothing to answer");

		// Turns inside one conversation are sequential, separate conversations run independently
		SemaphoreSlim gate = _turnLocks.GetOrAdd(request.Conversation, _ => new SemaphoreSlim(1, 1));
		await gate.WaitAsync(token);

		try
		{
			return await Exchange(request, origin, tools, model, token);
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

	private async Task<Result<string>> Exchange(AskRequest request, CommandOrigin origin, bool withTools, LlmFamily? model, CancellationToken token)
	{
		Conversation? conversation = request.Remember ? conversations.Load(request.Conversation) : null;
		IReadOnlyCollection<AiCapability> tools = withTools ? capabilities.For(request.Trusted) : [];

		SnapshotService snapshot = snapshots.Value;
		string mood = $"\n- Current mood: {await snapshot.Mood(token)}, because {await snapshot.MoodReason(token)}.";
		if (snapshot.Doing() is { Length: > 0 } doing) mood += $"\n- {doing}";

		if (request.Trusted) mood += $"\n- Where you believe gwynn7 is: {Standing()}";

		if (request.Trusted && Recollection(request.Message) is { Length: > 0 } known)
			mood += $"\n\nWhat you know about gwynn7:\n{known}";

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
			settings.Number(AiSettingKey.Temperature),
			model);

		Result<string> reply = await provider.Complete(aiRequest, call => Invoke(call, request.Trusted, origin, token), token);
		if (!reply.IsOk) return reply;

		if (!request.Remember) return reply;

		List<ChatMessage> messages =
		[
			.. conversation?.Messages ?? [],
			new ChatMessage(ChatRole.User, message, DateTimeOffset.Now, request.Author),
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
