using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Ai;

public enum ChatRole
{
	User,
	Assistant
}

/// Text keeps the author prefix the model reads; Author is kept apart so a client can strip it
public sealed record ChatMessage(ChatRole Role, string Text, DateTimeOffset At, string Author = "");

public sealed record Conversation(string Id, IReadOnlyList<ChatMessage> Messages, DateTimeOffset LastUsed);

public interface IConversationRepository
{
	Conversation? Load(string id);
	void Save(Conversation conversation);
	void Delete(string id);
	IReadOnlyList<string> Ids();
}

/// What the AI domain hands to a provider
/// Model is the family this one request wants, overriding the configured one. An unprompted line is
/// not worth a request from a family with five a day, and it is nobody's turn to wait on it
public sealed record AiRequest(
	string SystemPrompt,
	IReadOnlyList<ChatMessage> History,
	string Message,
	IReadOnlyList<AiToolDescriptor> Tools,
	double Temperature,
	LlmFamily? Model = null);

public sealed record AiToolDescriptor(string Name, string Description, IReadOnlyList<AiToolParameter> Parameters);

public sealed record AiToolParameter(string Name, string Description, AiParameterType Type, bool Required);

public enum AiParameterType
{
	String,
	Integer,
	Number,
	Boolean
}

public sealed record AiToolCall(string Name, IReadOnlyDictionary<string, string> Arguments);

public interface IAiProvider
{
	bool IsConfigured { get; }
	string ModelName { get; }
	IReadOnlyList<ModelView> Models();
	Result<string> SelectModel(string model);

	Task<Result<string>> Complete(AiRequest request, Func<AiToolCall, Task<string>> invokeTool, CancellationToken token = default);
}
