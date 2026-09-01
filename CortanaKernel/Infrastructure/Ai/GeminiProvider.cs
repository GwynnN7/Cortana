using System.Text.Json.Nodes;
using CortanaKernel.Domain.Ai;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Ai;

public sealed class GeminiProvider(ModelCatalogue catalogue, AiSettingsStore settings) : IAiProvider
{
	private const int MaxToolRounds = 6;

	private static readonly IReadOnlyDictionary<LlmFamily, string> DisplayNames = new Dictionary<LlmFamily, string>
	{
		[LlmFamily.Flash] = "Flash",
		[LlmFamily.FlashLite] = "Flash Lite",
		[LlmFamily.Gemma] = "Gemma"
	};

	private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(90) };

	public bool IsConfigured => !string.IsNullOrWhiteSpace(CortanaEnvironment.Read("CORTANA_GEMINI_KEY"));

	private LlmFamily Family => Enum.TryParse(settings.Model, true, out LlmFamily family) ? family : LlmFamily.FlashLite;

	public string ModelName => DisplayNames[Family];

	public IReadOnlyList<ModelView> Models() =>
	[
		.. DisplayNames.Select(entry => new ModelView(
			entry.Key, entry.Value, catalogue.Current(entry.Key), entry.Key == Family, catalogue.FamilyAvailable(entry.Key)))
	];

	public Result<string> SelectModel(string model)
	{
		string cleaned = model.Replace(" ", "");

		foreach ((LlmFamily family, string name) in DisplayNames)
		{
			if (!name.Replace(" ", "").Equals(cleaned, StringComparison.OrdinalIgnoreCase) &&
				!family.ToString().Equals(cleaned, StringComparison.OrdinalIgnoreCase)) continue;

			settings.SetModel(family.ToString());
			return Result.Ok($"Model set to {name} ({catalogue.Current(family)})");
		}

		return Result.Fail<string>($"Unknown model '{model}'. Valid models: {string.Join(", ", DisplayNames.Values)}");
	}

	public async Task<Result<string>> Complete(AiRequest request, Func<AiToolCall, Task<string>> invokeTool, CancellationToken token = default)
	{
		string? key = CortanaEnvironment.Read("CORTANA_GEMINI_KEY");
		if (string.IsNullOrWhiteSpace(key)) return Result.Fail<string>("CORTANA_GEMINI_KEY is not configured");

		JsonArray declarations = Declarations(request.Tools);
		var contents = new JsonArray();

		foreach (ChatMessage message in request.History)
			contents.Add(Turn(message.Role == ChatRole.User ? "user" : "model", message.Text));

		contents.Add(Turn("user", request.Message));

		for (var round = 0; round <= MaxToolRounds; round++)
		{
			var body = new JsonObject
			{
				["system_instruction"] = new JsonObject { ["parts"] = new JsonArray { new JsonObject { ["text"] = request.SystemPrompt } } },
				["contents"] = contents.DeepClone(),
				["generationConfig"] = new JsonObject { ["temperature"] = request.Temperature, ["maxOutputTokens"] = 1500 }
			};

			if (declarations.Count > 0)
				body["tools"] = new JsonArray { new JsonObject { ["functionDeclarations"] = declarations.DeepClone() } };

			(bool ok, string payload, int status) = await Send(body, key, token);
			if (!ok)
			{
				Log.Error("Ai", $"{status} {Detail(payload)}");
				return status is 429 or 503
					? Result.Fail<string>("Every model in this family is busy or rate limited, try again later or switch family")
					: Result.Fail<string>($"The language model returned {status}");
			}

			JsonNode root = JsonNode.Parse(payload) ?? new JsonObject();

			if (root["promptFeedback"]?["blockReason"]?.GetValue<string>() is { } blocked)
				return Result.Fail<string>($"The language model refused that one ({blocked})");

			if (root["candidates"] is not JsonArray { Count: > 0 } candidates) return Result.Fail<string>("The language model returned no answer");

			JsonNode? candidate = candidates[0];
			JsonArray parts = candidate?["content"]?["parts"]?.DeepClone().AsArray() ?? [];
			contents.Add(new JsonObject { ["role"] = "model", ["parts"] = parts.DeepClone() });

			JsonArray results = await RunTools(parts, invokeTool);
			if (results.Count > 0)
			{
				contents.Add(new JsonObject { ["role"] = "user", ["parts"] = results });
				continue;
			}

			if (candidate?["finishReason"]?.GetValue<string>() is { } finish and ("SAFETY" or "RECITATION" or "PROHIBITED_CONTENT"))
				return Result.Fail<string>($"The language model stopped early ({finish})");

			string reply = string.Concat(parts.Select(part => part?["text"]?.GetValue<string>()).Where(text => !string.IsNullOrEmpty(text)));
			return string.IsNullOrWhiteSpace(reply)
				? Result.Fail<string>("The language model returned an empty answer")
				: Result.Ok(reply.Trim());
		}

		return Result.Fail<string>("I got stuck checking the house, try asking again");
	}

	private async Task<(bool Ok, string Payload, int Status)> Send(JsonObject body, string key, CancellationToken token)
	{
		string model = catalogue.Current(Family);
		var attempts = 0;

		while (true)
		{
			using HttpResponseMessage response = await _http.PostAsJsonAsync(
				$"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}", body, token);

			string payload = await response.Content.ReadAsStringAsync(token);
			if (response.IsSuccessStatusCode) return (true, payload, 200);

			var status = (int)response.StatusCode;
			if (status is not (429 or 503) || ++attempts > catalogue.Chain(Family).Length) return (false, payload, status);

			catalogue.Penalise(model, payload);

			if (catalogue.Next(Family, model) is not { } fallback) return (false, payload, status);

			Log.Write("Ai", $"Falling back to {fallback}");
			model = fallback;
		}
	}

	private static async Task<JsonArray> RunTools(JsonArray parts, Func<AiToolCall, Task<string>> invokeTool)
	{
		var results = new JsonArray();

		foreach (JsonNode? part in parts)
		{
			if (part?["functionCall"] is not JsonObject call) continue;

			string name = call["name"]?.GetValue<string>() ?? "";
			var arguments = new Dictionary<string, string>();

			if (call["args"] is JsonObject raw)
				foreach ((string key, JsonNode? value) in raw)
					arguments[key] = value?.GetValueKind() == System.Text.Json.JsonValueKind.String
						? value.GetValue<string>()
						: value?.ToJsonString().Trim('"') ?? "";

			string outcome = await invokeTool(new AiToolCall(name, arguments));

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

	private static JsonObject Turn(string role, string text) =>
		new() { ["role"] = role, ["parts"] = new JsonArray { new JsonObject { ["text"] = text } } };

	/// Gemini wants a trimmed JSON Schema so the descriptors are translated
	private static JsonArray Declarations(IReadOnlyList<AiToolDescriptor> tools)
	{
		var declarations = new JsonArray();

		foreach (AiToolDescriptor tool in tools)
		{
			var declaration = new JsonObject { ["name"] = tool.Name, ["description"] = tool.Description };

			if (tool.Parameters.Count > 0)
			{
				var properties = new JsonObject();
				var required = new JsonArray();

				foreach (AiToolParameter parameter in tool.Parameters)
				{
					properties[parameter.Name] = new JsonObject
					{
						["type"] = parameter.Type switch
						{
							AiParameterType.Integer => "integer",
							AiParameterType.Number => "number",
							AiParameterType.Boolean => "boolean",
							_ => "string"
						},
						["description"] = parameter.Description
					};

					if (parameter.Required) required.Add(parameter.Name);
				}

				var schema = new JsonObject { ["type"] = "object", ["properties"] = properties };
				if (required.Count > 0) schema["required"] = required;
				declaration["parameters"] = schema;
			}

			declarations.Add(declaration);
		}

		return declarations;
	}

	private static string Detail(string payload)
	{
		try
		{
			return JsonNode.Parse(payload)?["error"]?["message"]?.GetValue<string>() ?? payload;
		}
		catch (System.Text.Json.JsonException)
		{
			return payload;
		}
	}
}
