namespace CortanaLib.Contracts;

/// A persistent conversation turn. `Ask` uses the same pipeline with Remember = false
public sealed record AskRequest(
	string Message,
	string Conversation = "default",
	string Author = "",
	bool Remember = true,
	bool Trusted = true);

public sealed record AskResponse(string Reply, string Conversation);

/// One stored turn, as any client would render it
public sealed record ChatTurn(bool Mine, string Text, DateTimeOffset At);

public sealed record ConversationResponse(string Conversation, IReadOnlyList<ChatTurn> Turns);

public static class Conversations
{
	/// Every browser shares one conversation, so the dashboard is a channel Cortana can speak into
	public const string Web = "web";
}

public enum LlmFamily
{
	Flash,
	FlashLite,
	Gemma
}

public sealed record ModelView(LlmFamily Family, string Name, string ModelId, bool Current, bool Available);

public sealed record ModelListResponse(IReadOnlyList<ModelView> Models, string Current);

public sealed record ModelRequest(string Model);

public sealed record PromptRequest(string Prompt);

public enum AiSettingKey
{
	Temperature,
	RememberedExchanges,
	DiscordSessionMinutes,
	HistorySampleMinutes,
	HistoryRetentionDays,
	PushEventSeconds,
	MemoryDepth,
	MemoryStateHours,
	WrapupHour,
	WrapupChance
}

public sealed record AiSettingView(AiSettingKey Setting, string Value);

public sealed record AiSettingListResponse(IReadOnlyList<AiSettingView> Settings);

public sealed record NumberRequest(double Value);

public sealed record VolitionState(
	DateTimeOffset? QuietUntil = null,
	DateOnly? LastGreeted = null,
	DateTimeOffset? LastSpokeAt = null,
	DateOnly? LastWrapped = null);

public sealed record QuietRequest(int Minutes);
