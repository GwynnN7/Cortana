using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static partial class ModelCatalogue
{
	private static readonly TimeSpan MinuteCooldown = TimeSpan.FromSeconds(90);
	private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(20);
	private static readonly string[] Excluded = ["tts", "image", "embedding", "omni", "vision", "live", "native-audio", "latest"];

	private static readonly IReadOnlyDictionary<ELlmModel, string[]> Fallback = new Dictionary<ELlmModel, string[]>
	{
		[ELlmModel.Flash] = ["gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3-flash-preview"],
		[ELlmModel.FlashLite] = ["gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-2.5-flash-lite"],
		[ELlmModel.Gemma] = ["gemma-4-31b-it", "gemma-4-26b-a4b-it"]
	};

	private static readonly ConcurrentDictionary<string, DateTime> Cooldowns = new();
	private static readonly Lock Gate = new();
	private static Dictionary<ELlmModel, string[]> _chains = Fallback.ToDictionary(entry => entry.Key, entry => entry.Value);

	[GeneratedRegex(@"(\d+(?:\.\d+)?)")] private static partial Regex Version { get; }
	[GeneratedRegex(@"(\d+)b")] private static partial Regex Size { get; }

	public static string[] Chain(ELlmModel family)
	{
		lock (Gate) return _chains.TryGetValue(family, out string[]? chain) && chain.Length > 0 ? chain : Fallback[family];
	}

	public static string Current(ELlmModel family)
	{
		string[] chain = Chain(family);
		string? ready = chain.FirstOrDefault(Available);

		return ready ?? chain.MinBy(model => Cooldowns.GetValueOrDefault(model, DateTime.MinValue)) ?? chain[0];
	}

	public static bool Available(string model) =>
		!Cooldowns.TryGetValue(model, out DateTime until) || until <= DateTime.Now;

	public static string? Next(ELlmModel family, string exhausted)
	{
		string[] chain = Chain(family);
		int index = Array.IndexOf(chain, exhausted);
		if (index < 0) return chain.FirstOrDefault(Available);

		return chain.Skip(index + 1).FirstOrDefault(Available);
	}

	public static void Penalise(string model, string payload)
	{
		TimeSpan cooldown = ReadCooldown(payload);
		Cooldowns[model] = DateTime.Now + cooldown;

		DataHandler.Log($"[LLM] {model} unavailable, parked for {Describe(cooldown)}");
	}

	private static string Describe(TimeSpan span) =>
		span < TimeSpan.FromMinutes(2) ? $"{span.TotalSeconds:F0}s" : $"{span.TotalHours:F1}h";

	private static TimeSpan ReadCooldown(string payload)
	{
		try
		{
			JsonNode? details = JsonNode.Parse(payload)?["error"]?["details"];
			if (details is JsonArray entries)
			{
				var perDay = false;
				TimeSpan? retry = null;

				foreach (JsonNode? entry in entries)
				{
					string type = entry?["@type"]?.GetValue<string>() ?? "";

					if (type.EndsWith("QuotaFailure", StringComparison.Ordinal)
						&& entry?["violations"] is JsonArray violations
						&& violations.Any(violation => violation?["quotaId"]?.GetValue<string>()?.Contains("PerDay", StringComparison.OrdinalIgnoreCase) == true))
						perDay = true;

					if (type.EndsWith("RetryInfo", StringComparison.Ordinal)
						&& entry?["retryDelay"]?.GetValue<string>() is { Length: > 1 } delay
						&& double.TryParse(delay.TrimEnd('s'), CultureInfo.InvariantCulture, out double seconds))
						retry = TimeSpan.FromSeconds(seconds + 2);
				}

				if (perDay) return UntilTomorrow();
				if (retry is { } value) return value;
			}
		}
		catch (Exception)
		{
		}

		return payload.Contains("PerDay", StringComparison.OrdinalIgnoreCase) ? UntilTomorrow() : MinuteCooldown;
	}

	private static TimeSpan UntilTomorrow() => DateTime.Today.AddDays(1).AddMinutes(1) - DateTime.Now;

	public static void Start()
	{
		_ = Task.Run(async () =>
		{
			await Refresh();

			while (true)
			{
				await Task.Delay(UntilTomorrow());
				await Refresh();
			}
		});
	}

	public static async Task Refresh()
	{
		string? key = DataHandler.EnvOrNull("CORTANA_GEMINI_KEY");
		if (string.IsNullOrWhiteSpace(key)) return;

		try
		{
			using var client = new HttpClient { Timeout = RefreshTimeout };
			string payload = await client.GetStringAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={key}");

			if (JsonNode.Parse(payload)?["models"] is not JsonArray models) return;

			List<string> usable = models
				.Select(model => model?["name"]?.GetValue<string>()?.Replace("models/", ""))
				.Where(name => !string.IsNullOrEmpty(name))
				.Select(name => name!)
				.Where(name => !Excluded.Any(bad => name.Contains(bad, StringComparison.OrdinalIgnoreCase)))
				.Where(name => models.Any(model =>
					model?["name"]?.GetValue<string>()?.EndsWith(name, StringComparison.Ordinal) == true
					&& model["supportedGenerationMethods"] is JsonArray methods
					&& methods.Any(method => method?.GetValue<string>() == "generateContent")))
				.ToList();

			var rebuilt = new Dictionary<ELlmModel, string[]>
			{
				[ELlmModel.Gemma] = Order(usable.Where(name => name.StartsWith("gemma", StringComparison.Ordinal)), true),
				[ELlmModel.FlashLite] = Order(usable.Where(name => name.Contains("flash-lite", StringComparison.Ordinal)), false),
				[ELlmModel.Flash] = Order(usable.Where(name =>
					name.Contains("flash", StringComparison.Ordinal) && !name.Contains("flash-lite", StringComparison.Ordinal)), false)
			};

			lock (Gate)
			{
				foreach ((ELlmModel family, string[] chain) in rebuilt)
					if (chain.Length > 0) _chains[family] = chain;
			}

			Cooldowns.Clear();
			DataHandler.Log($"[LLM] Model table refreshed: {string.Join(" | ", rebuilt.Select(entry => $"{entry.Key} {entry.Value.FirstOrDefault()}"))}");
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[LLM] Could not refresh the model table: {ex.Message}");
		}
	}

	private static string[] Order(IEnumerable<string> names, bool bySize) =>
		names.OrderByDescending(name => bySize ? Number(Size, name) : Number(Version, name))
			.ThenBy(name => name.Contains("preview", StringComparison.Ordinal))
			.ThenBy(name => name)
			.ToArray();

	private static double Number(Regex pattern, string name)
	{
		Match match = pattern.Match(name);
		return match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out double value) ? value : 0;
	}
}
