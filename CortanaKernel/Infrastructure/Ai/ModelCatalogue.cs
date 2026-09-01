using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CortanaLib.Contracts;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Ai;

/// Provider-specific model ids, ranked per family, refreshed daily and removed when rate limited
public sealed partial class ModelCatalogue : BackgroundService
{
	private static readonly TimeSpan MinuteCooldown = TimeSpan.FromSeconds(90);
	private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(20);
	private static readonly string[] Excluded = ["tts", "image", "embedding", "omni", "vision", "live", "native-audio", "latest"];

	private static readonly IReadOnlyDictionary<LlmFamily, string[]> Fallback = new Dictionary<LlmFamily, string[]>
	{
		[LlmFamily.Flash] = ["gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3-flash-preview"],
		[LlmFamily.FlashLite] = ["gemini-3.5-flash-lite", "gemini-3.1-flash-lite", "gemini-2.5-flash-lite"],
		[LlmFamily.Gemma] = ["gemma-4-31b-it", "gemma-4-26b-a4b-it"]
	};

	private readonly ConcurrentDictionary<string, DateTime> _cooldowns = new();
	private readonly Lock _gate = new();
	private Dictionary<LlmFamily, string[]> _chains = Fallback.ToDictionary(entry => entry.Key, entry => entry.Value);

	[GeneratedRegex(@"(\d+(?:\.\d+)?)")] private static partial Regex Version { get; }
	[GeneratedRegex(@"(\d+)b")] private static partial Regex Size { get; }

	public string[] Chain(LlmFamily family)
	{
		lock (_gate) return _chains.TryGetValue(family, out string[]? chain) && chain.Length > 0 ? chain : Fallback[family];
	}

	public string Current(LlmFamily family)
	{
		string[] chain = Chain(family);
		return chain.FirstOrDefault(Available) ?? chain.MinBy(model => _cooldowns.GetValueOrDefault(model, DateTime.MinValue)) ?? chain[0];
	}

	public bool Available(string model) => !_cooldowns.TryGetValue(model, out DateTime until) || until <= DateTime.Now;

	public bool FamilyAvailable(LlmFamily family) => Chain(family).Any(Available);

	public string? Next(LlmFamily family, string exhausted)
	{
		string[] chain = Chain(family);
		int index = Array.IndexOf(chain, exhausted);
		return index < 0 ? chain.FirstOrDefault(Available) : chain.Skip(index + 1).FirstOrDefault(Available);
	}

	public void Penalise(string model, string payload)
	{
		TimeSpan cooldown = ReadCooldown(payload);
		_cooldowns[model] = DateTime.Now + cooldown;

		Log.Write("Ai", $"{model} is unavailable, parked for {Describe(cooldown)}");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Refresh(stoppingToken);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(UntilTomorrow(), stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			await Refresh(stoppingToken);
		}
	}

	public async Task Refresh(CancellationToken token = default)
	{
		string? key = CortanaEnvironment.Read("CORTANA_GEMINI_KEY");
		if (string.IsNullOrWhiteSpace(key)) return;

		try
		{
			using var client = new HttpClient { Timeout = RefreshTimeout };
			string payload = await client.GetStringAsync($"https://generativelanguage.googleapis.com/v1beta/models?key={key}", token);

			if (JsonNode.Parse(payload)?["models"] is not JsonArray models) return;

			List<string> usable =
			[
				.. models
					.Where(model => model?["supportedGenerationMethods"] is JsonArray methods
						&& methods.Any(method => method?.GetValue<string>() == "generateContent"))
					.Select(model => model?["name"]?.GetValue<string>()?.Replace("models/", ""))
					.OfType<string>()
					.Where(name => !Excluded.Any(bad => name.Contains(bad, StringComparison.OrdinalIgnoreCase)))
			];

			var rebuilt = new Dictionary<LlmFamily, string[]>
			{
				[LlmFamily.Gemma] = Order(usable.Where(name => name.StartsWith("gemma", StringComparison.Ordinal)), bySize: true),
				[LlmFamily.FlashLite] = Order(usable.Where(name => name.Contains("flash-lite", StringComparison.Ordinal)), bySize: false),
				[LlmFamily.Flash] = Order(usable.Where(name =>
					name.Contains("flash", StringComparison.Ordinal) && !name.Contains("flash-lite", StringComparison.Ordinal)), bySize: false)
			};

			lock (_gate)
				foreach ((LlmFamily family, string[] chain) in rebuilt)
					if (chain.Length > 0) _chains[family] = chain;

			_cooldowns.Clear();
			Log.Write("Ai", $"Model table refreshed: {string.Join(" | ", rebuilt.Select(entry => $"{entry.Key} {entry.Value.FirstOrDefault()}"))}");
		}
		catch (Exception ex)
		{
			Log.Write("Ai", $"Could not refresh the model table: {ex.Message}");
		}
	}

	private static string[] Order(IEnumerable<string> names, bool bySize) =>
	[
		.. names
			.OrderByDescending(name => Number(bySize ? Size : Version, name))
			.ThenBy(name => name.Contains("preview", StringComparison.Ordinal))
			.ThenBy(name => name)
	];

	private static double Number(Regex pattern, string name)
	{
		Match match = pattern.Match(name);
		return match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out double value) ? value : 0;
	}

	private static string Describe(TimeSpan span) => span < TimeSpan.FromMinutes(2) ? $"{span.TotalSeconds:F0}s" : $"{span.TotalHours:F1}h";

	private static TimeSpan UntilTomorrow() => DateTime.Today.AddDays(1).AddMinutes(1) - DateTime.Now;

	/// Google says how long to wait
	private static TimeSpan ReadCooldown(string payload)
	{
		try
		{
			if (JsonNode.Parse(payload)?["error"]?["details"] is JsonArray entries)
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
		catch (Exception) { }

		return payload.Contains("PerDay", StringComparison.OrdinalIgnoreCase) ? UntilTomorrow() : MinuteCooldown;
	}
}
