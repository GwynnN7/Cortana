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
	/// What a limit turned out to be. Daily means the model is spent; anything else is a queue it is
	/// standing in, and it will take requests again in seconds
	public sealed record Limit(TimeSpan Wait, bool Daily, string Reason);

	/// A per-minute limit clears inside a minute, and Google usually says exactly when. Parking for
	/// longer than it lasts is what handed a whole day of the weaker models away one burst at a time
	private static readonly TimeSpan BriefCooldown = TimeSpan.FromSeconds(10);
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

	public Limit Penalise(string model, string payload)
	{
		Limit limit = ReadLimit(payload);
		_cooldowns[model] = DateTime.Now + limit.Wait;

		Log.Write("Ai", $"{model} hit a {limit.Reason} limit, parked for {Describe(limit.Wait)}");
		return limit;
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

	/// Google says which quota was hit and how long to wait, and the difference matters: a day is the
	/// model spent, a minute is the model busy. The quota id is kept for the log, because a 429 nobody
	/// can name is the reason this was tuned blind for so long
	private static Limit ReadLimit(string payload)
	{
		try
		{
			if (JsonNode.Parse(payload)?["error"]?["details"] is JsonArray entries)
			{
				// Every violation has to be tested, not just the last one seen: Google lists the per-day and
				// the per-minute quota together, and reading only the last would call a spent model merely busy
				var perDay = false;
				var quota = "";
				TimeSpan? retry = null;

				foreach (JsonNode? entry in entries)
				{
					string type = entry?["@type"]?.GetValue<string>() ?? "";

					if (type.EndsWith("QuotaFailure", StringComparison.Ordinal) && entry?["violations"] is JsonArray violations)
						foreach (JsonNode? violation in violations)
							if (violation?["quotaId"]?.GetValue<string>() is { Length: > 0 } id)
							{
								perDay |= id.Contains("PerDay", StringComparison.OrdinalIgnoreCase);
								quota = id;
							}

					if (type.EndsWith("RetryInfo", StringComparison.Ordinal)
						&& entry?["retryDelay"]?.GetValue<string>() is { Length: > 1 } delay
						&& double.TryParse(delay.TrimEnd('s'), CultureInfo.InvariantCulture, out double seconds))
						retry = TimeSpan.FromSeconds(seconds + 2);
				}

				if (perDay) return new Limit(UntilTomorrow(), true, quota.Length > 0 ? $"daily/{quota}" : "daily");
				if (retry is { } value) return new Limit(value, false, quota.Length > 0 ? quota : "rate");
				if (quota.Length > 0) return new Limit(BriefCooldown, false, quota);
			}
		}
		catch (Exception) { }

		return payload.Contains("PerDay", StringComparison.OrdinalIgnoreCase)
			? new Limit(UntilTomorrow(), true, "daily")
			: new Limit(BriefCooldown, false, "unnamed");
	}
}
