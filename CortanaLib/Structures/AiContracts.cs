namespace CortanaLib.Structures;

public record PostChat(string Message, string Conversation = "default", string Author = "", bool Remember = true, bool Owner = true);
public record ChatResponse(string Reply, string Conversation) : IApiResponse;

public enum ELlmModel
{
	Flash,
	FlashLite,
	Gemma
}

public record ModelResponse(string Name, string Id, bool Current, bool Available) : IApiResponse;
public record ModelListResponse(IReadOnlyList<ModelResponse> Models, string Current) : IApiResponse;
public record PostModel(string Model);
public record PostPrompt(string Prompt);
public record PostNumber(double Value);

public enum EAiSetting
{
	Temperature,
	History,
	DiscordMinutes
}

public record AiSettingResponse(string Setting, string Value) : IApiResponse;
public record AiSettingsListResponse(IReadOnlyList<AiSettingResponse> Settings) : IApiResponse;
