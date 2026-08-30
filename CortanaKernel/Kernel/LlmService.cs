using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using CortanaLib;
using CortanaLib.Structures;
using Microsoft.Extensions.AI;

namespace CortanaKernel.Kernel;

public static class LlmService
{
	private const string Untrusted = "discord:";
	private const string Owned = "- You are talking to gwynn7 now.";

	private static string Guest(string author) =>
		$"- You are talking to a guest ({author}), not gwynn7. Stay friendly and helpful, but keep it in mind.";

	private const string Restriction = "- In this conversation your tools are read-only: you can look at the house and the computer but not change them. If (and only if) someone asks you to activate something, say you can't from here.";
	private const int MaxEntries = 60;
	private const int MaxConversations = 64;
	private const int MaxToolRounds = 5;
	private static readonly TimeSpan IdleExpiry = TimeSpan.FromHours(6);
	private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

	private static readonly string PromptPath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Prompt.txt");
	private static readonly HttpClient Client = new() { Timeout = Timeout };
	private static readonly ConcurrentDictionary<string, Conversation> Conversations = new();
	private static readonly Lazy<JsonArray> FullDeclarations = new(() => BuildDeclarations(LlmTools.All));
	private static readonly Lazy<JsonArray> SafeDeclarations = new(() => BuildDeclarations(LlmTools.ReadOnly));

	private static readonly IReadOnlyDictionary<ELlmModel, string> Names = new Dictionary<ELlmModel, string>
	{
		[ELlmModel.Flash] = "Flash",
		[ELlmModel.FlashLite] = "Flash Lite",
		[ELlmModel.Gemma] = "Gemma"
	};

	public static ELlmModel Model => AiSettings.Model;

	public static string ModelName => Names[Model];

	public static string ModelId => ModelCatalogue.Current(Model);

	public static IReadOnlyList<ModelResponse> Models() =>
		Names.Select(entry => new ModelResponse(
			entry.Value,
			ModelCatalogue.Current(entry.Key),
			entry.Key == Model,
			ModelCatalogue.Chain(entry.Key).Any(ModelCatalogue.Available))).ToList();

	public static bool TryParseModel(string value, out ELlmModel model)
	{
		string cleaned = value.Replace(" ", "");

		foreach ((ELlmModel key, string name) in Names)
		{
			if (!name.Replace(" ", "").Equals(cleaned, StringComparison.OrdinalIgnoreCase) &&
				!key.ToString().Equals(cleaned, StringComparison.OrdinalIgnoreCase)) continue;

			model = key;
			return true;
		}

		model = default;
		return false;
	}

	public static StringResult SetModel(ELlmModel model)
	{
		StringResult saved = AiSettings.SetModel(model);
		return saved.IsOk ? StringResult.Success($"Model set to {Names[model]} ({ModelCatalogue.Current(model)})") : saved;
	}

	public static bool IsConfigured => !string.IsNullOrWhiteSpace(DataHandler.EnvOrNull("CORTANA_GEMINI_KEY"));

	private static string ShippedPrompt => Path.Combine(DataHandler.CortanaPath(EDirType.Storage), "prompt.txt");

	public static bool PromptIsCustom => File.Exists(PromptPath);

	public static string SystemPrompt
	{
		get
		{
			if (File.Exists(PromptPath)) return File.ReadAllText(PromptPath);

			return File.Exists(ShippedPrompt) ? File.ReadAllText(ShippedPrompt) : "You are Cortana, a concise and witty home assistant.";
		}
	}

	public static StringResult SetPrompt(string prompt)
	{
		if (string.IsNullOrWhiteSpace(prompt)) return StringResult.Failure("The prompt cannot be empty");

		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PromptPath)!);
			File.WriteAllText(PromptPath, prompt.Trim());

			return StringResult.Success("Prompt saved");
		}
		catch (Exception ex)
		{
			return StringResult.Failure($"Could not save the prompt: {ex.Message}");
		}
	}

	public static StringResult ResetPrompt()
	{
		try
		{
			if (File.Exists(PromptPath)) File.Delete(PromptPath);

			return StringResult.Success("Prompt restored to the shipped one");
		}
		catch (Exception ex)
		{
			return StringResult.Failure($"Could not restore the prompt: {ex.Message}");
		}
	}

	public static void ResetConversation(string conversation) => Conversations.TryRemove(conversation, out _);

	private sealed class Conversation
	{
		public JsonArray Contents { get; set; } = [];
		public DateTime LastUsed { get; set; } = DateTime.UtcNow;
		public SemaphoreSlim Gate { get; } = new(1, 1);
	}

	public static async Task<StringResult> Ask(string conversation, string message, string author, bool remember = true, bool owner = true)
	{
		string? key = DataHandler.EnvOrNull("CORTANA_GEMINI_KEY");
		if (string.IsNullOrWhiteSpace(key)) return StringResult.Failure("CORTANA_GEMINI_KEY is not configured");
		if (string.IsNullOrWhiteSpace(message)) return StringResult.Failure("Nothing to answer");

		if (!remember) return await Guarded(null, conversation, message, author, key, owner);

		Evict();

		Conversation chat = Conversations.GetOrAdd(conversation, _ => new Conversation());
		await chat.Gate.WaitAsync();
		try
		{
			return await Guarded(chat, conversation, message, author, key, owner);
		}
		finally
		{
			chat.LastUsed = DateTime.UtcNow;
			chat.Gate.Release();
		}
	}

	private static async Task<StringResult> Guarded(Conversation? chat, string conversation, string message, string author, string key, bool owner)
	{
		try
		{
			return await Exchange(chat, conversation, message, author, key, owner);
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[LLM] {ex.Message}");
			return StringResult.Failure("I couldn't reach my language model");
		}
	}

	private static void Evict()
	{
		DateTime cutoff = DateTime.UtcNow - IdleExpiry;

		foreach ((string key, Conversation chat) in Conversations)
			if (chat.LastUsed < cutoff) Conversations.TryRemove(key, out _);

		while (Conversations.Count > MaxConversations)
		{
			string? oldest = Conversations.MinBy(entry => entry.Value.LastUsed).Key;
			if (oldest is null || !Conversations.TryRemove(oldest, out _)) break;
		}
	}

	private static async Task<StringResult> Exchange(Conversation? chat, string conversation, string message, string author, string key, bool owner)
	{
		IReadOnlyDictionary<string, AIFunction> tools = LlmTools.For(conversation);
		bool restricted = ReferenceEquals(tools, LlmTools.ReadOnly);
		JsonArray declarations = (restricted ? SafeDeclarations : FullDeclarations).Value;
		bool trusted = !conversation.StartsWith(Untrusted, StringComparison.OrdinalIgnoreCase) || owner;
		var instructions = $"{SystemPrompt}\n{(trusted ? Owned : Guest(author))}";
		if (restricted) instructions += $"\n{Restriction}";

		JsonArray spoken = chat?.Contents.DeepClone().AsArray() ?? [];
		JsonObject question = Turn("user", new JsonObject { ["text"] = string.IsNullOrWhiteSpace(author) ? message : $"{author}: {message}" });

		var contents = spoken.DeepClone().AsArray();
		contents.Add(question.DeepClone());

		for (var round = 0; round <= MaxToolRounds; round++)
		{
			var body = new JsonObject
			{
				["system_instruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = instructions } } },
				["contents"] = contents.DeepClone(),
				["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = declarations.DeepClone() } },
				["generationConfig"] = new JsonObject { ["temperature"] = AiSettings.Temperature, ["maxOutputTokens"] = 1500 }
			};

			(bool ok, string payload, int status) = await Send(body, key);
			if (!ok)
			{
				DataHandler.Log($"[LLM] {status} {Detail(payload)}");
				return status is 429 or 503
					? StringResult.Failure("Every model in this family is busy or rate limited, try again later or switch family")
					: StringResult.Failure($"Gemini returned {status}");
			}

			JsonNode root = JsonNode.Parse(payload) ?? new JsonObject();

			if (root["promptFeedback"]?["blockReason"]?.GetValue<string>() is { } blocked)
				return StringResult.Failure($"Gemini refused that one ({blocked})");

			if (root["candidates"] is not JsonArray { Count: > 0 } candidates)
				return StringResult.Failure("Gemini returned no answer");

			JsonNode? candidate = candidates[0];
			JsonArray parts = candidate?["content"]?["parts"]?.DeepClone().AsArray() ?? [];
			contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts.DeepClone() });

			JsonArray results = await Invoke(parts, tools);
			if (results.Count > 0)
			{
				contents.Add(new JsonObject { ["role"] = "user", ["parts"] = results });
				continue;
			}

			if (candidate?["finishReason"]?.GetValue<string>() is { } finish and ("SAFETY" or "RECITATION" or "PROHIBITED_CONTENT"))
				return StringResult.Failure($"Gemini stopped early ({finish})");

			string reply = string.Concat(parts
				.Select(part => part?["text"]?.GetValue<string>())
				.Where(text => !string.IsNullOrEmpty(text)));

			if (string.IsNullOrWhiteSpace(reply)) return StringResult.Failure("Gemini returned an empty answer");

			if (chat is not null)
			{
				spoken.Add(question);
				spoken.Add(Turn("model", new JsonObject { ["text"] = reply.Trim() }));
				chat.Contents = Trim(spoken);
			}

			return StringResult.Success(reply.Trim());
		}

		return StringResult.Failure("I got stuck checking the house, try asking again");
	}

	private static async Task<(bool Ok, string Payload, int Status)> Send(JsonObject body, string key)
	{
		string model = ModelId;
		var attempts = 0;

		while (true)
		{
			using HttpResponseMessage response = await Client.PostAsJsonAsync(
				$"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}", body);

			string payload = await response.Content.ReadAsStringAsync();
			if (response.IsSuccessStatusCode) return (true, payload, 200);

			var status = (int)response.StatusCode;
			if (status is not (429 or 503) || ++attempts > ModelCatalogue.Chain(Model).Length)
				return (false, payload, status);

			ModelCatalogue.Penalise(model, payload);

			if (ModelCatalogue.Next(Model, model) is not { } fallback) return (false, payload, status);

			DataHandler.Log($"[LLM] falling back to {fallback}");
			model = fallback;
		}
	}

	private static async Task<JsonArray> Invoke(JsonArray parts, IReadOnlyDictionary<string, AIFunction> tools)
	{
		var results = new JsonArray();

		foreach (JsonNode? part in parts)
		{
			if (part?["functionCall"] is not JsonObject call) continue;

			string name = call["name"]?.GetValue<string>() ?? "";
			string outcome;

			if (!tools.TryGetValue(name, out AIFunction? function))
			{
				outcome = $"Unknown tool '{name}'";
			}
			else
			{
				try
				{
					Dictionary<string, object?> arguments = call["args"] is JsonObject raw
						? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(raw.ToJsonString())!
							.ToDictionary(entry => entry.Key, object? (entry) => entry.Value)
						: [];

					object? value = await function.InvokeAsync(new AIFunctionArguments(arguments));
					outcome = Text(value);
				}
				catch (Exception ex)
				{
					DataHandler.Log($"[LLM] {name} failed: {ex.Message}");
					outcome = $"'{name}' failed: {ex.Message}";
				}
			}

			results.Add(new JsonObject
			{
				["functionResponse"] = new JsonObject
				{
					["name"] = name,
					["response"] = new JsonObject { ["result"] = outcome }
				}
			});
		}

		return results;
	}

	private static string Text(object? value) => value switch
	{
		null => "",
		string text => text,
		JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? "",
		JsonElement element => element.GetRawText(),
		_ => value.ToString() ?? ""
	};

	private static JsonObject Turn(string role, JsonObject part) =>
		new() { ["role"] = role, ["parts"] = new JsonArray { part } };

	private static JsonArray Trim(JsonArray contents)
	{
		var trimmed = contents.DeepClone().AsArray();
		int keep = AiSettings.History;

		List<int> starts = Enumerable.Range(0, trimmed.Count).Where(index => IsPlainUserTurn(trimmed[index])).ToList();
		if (starts.Count > keep)
			for (var drop = 0; drop < starts[starts.Count - keep]; drop++) trimmed.RemoveAt(0);

		while (trimmed.Count > MaxEntries) trimmed.RemoveAt(0);
		while (trimmed.Count > 0 && !IsPlainUserTurn(trimmed[0])) trimmed.RemoveAt(0);

		return trimmed;
	}

	private static bool IsPlainUserTurn(JsonNode? content)
	{
		if (content?["role"]?.GetValue<string>() != "user") return false;

		return content["parts"] is not JsonArray parts || parts.All(part => part?["functionResponse"] is null);
	}

	private static string Detail(string payload)
	{
		try
		{
			return JsonNode.Parse(payload)?["error"]?["message"]?.GetValue<string>() ?? payload;
		}
		catch (JsonException)
		{
			return payload;
		}
	}

	private static JsonArray BuildDeclarations(IReadOnlyDictionary<string, AIFunction> tools)
	{
		var declarations = new JsonArray();

		foreach ((string name, AIFunction function) in tools)
		{
			JsonNode? schema = Sanitize(JsonNode.Parse(function.JsonSchema.GetRawText()));
			if (schema?["properties"] is not JsonObject { Count: > 0 }) schema = null;

			var declaration = new JsonObject { ["name"] = name, ["description"] = function.Description };
			if (schema is not null) declaration["parameters"] = schema;

			declarations.Add(declaration);
		}

		return declarations;
	}

	private static JsonNode? Sanitize(JsonNode? node)
	{
		switch (node)
		{
			case JsonObject obj:
			{
				foreach (string unsupported in (string[])["$schema", "additionalProperties", "default", "format", "exclusiveMinimum", "exclusiveMaximum"])
					obj.Remove(unsupported);

				if (obj["type"] is JsonArray types)
					obj["type"] = types.Select(type => type?.GetValue<string>()).FirstOrDefault(type => type is not null and not "null");

				foreach ((string key, JsonNode? child) in obj.ToList()) obj[key] = Sanitize(child?.DeepClone());
				break;
			}
			case JsonArray array:
			{
				for (var index = 0; index < array.Count; index++) array[index] = Sanitize(array[index]?.DeepClone());
				break;
			}
		}

		return node;
	}
}
