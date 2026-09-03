using CortanaKernel.Domain.Settings;
using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.History;
using CortanaKernel.Domain.Fabric;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed class HistoryService(
	IHistoryRepository repository,
	IRhythmRepository rhythm,
	Fabric sensors,
	DeviceService devices,
	ActivityRegistry activity,
	MemoryStore memories,
	AiSettingsStore aiSettings,
	SettingsStore flags) : BackgroundService
{
	private const int MaxSamples = 240;

	public IReadOnlyList<string> Metrics => repository.Metrics;

	public HistoryInfoResponse Info() => new(
		repository.Metrics,
		aiSettings.Integer(AiSettingKey.HistoryRetentionDays),
		aiSettings.Integer(AiSettingKey.HistorySampleMinutes),
		repository.DiskUsage());

	public Result<HistorySeries> Series(string metric, int hours, DateTimeOffset? until = null)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		if (!repository.Metrics.Contains(wanted))
			return Result.Fail<HistorySeries>($"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}");

		DateTimeOffset to = until ?? DateTimeOffset.Now;
		if (to > DateTimeOffset.Now) to = DateTimeOffset.Now;
		DateTimeOffset from = to.AddHours(-Math.Clamp(hours, 1, 24 * 365));

		IReadOnlyList<HistoryPoint> points = repository.Read(wanted, from, to);

		if (points.Count == 0)
			return Result.Ok(new HistorySeries(wanted, Unit(wanted), 0, 0, 0, 0, from, to, []));

		return Result.Ok(new HistorySeries(
			wanted,
			Unit(wanted),
			points.Count,
			Math.Round(points.Min(point => point.Value), 1),
			Math.Round(points.Max(point => point.Value), 1),
			Math.Round(points.Average(point => point.Value), 1),
			from,
			to,
			Thin(points)));
	}

	public Result<AnalysisResult> Analyse(AnalysisRequest request)
	{
		string metric = request.Metric.Trim().ToLowerInvariant();
		if (!repository.Metrics.Contains(metric))
			return Result.Fail<AnalysisResult>($"Unknown metric '{request.Metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}");

		IReadOnlyList<HistoryPoint> points = repository.Read(metric, request.From, request.To);
		IReadOnlyList<HistoryPoint> comparison = request is { CompareFrom: { } from, CompareTo: { } to }
			? repository.Read(metric, from, to)
			: [];

		return Result.Ok(HistoryAnalysis.Run(
			request.Function, metric, Unit(metric), points, comparison, request.At, request.State, request.WindowMinutes));
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		repository.Prune(aiSettings.Integer(AiSettingKey.HistoryRetentionDays));

		while (!stoppingToken.IsCancellationRequested)
		{
			TimeSpan every = TimeSpan.FromMinutes(Math.Clamp(aiSettings.Integer(AiSettingKey.HistorySampleMinutes), 1, 60));

			try
			{
				await Task.Delay(every, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			if (!flags.Flag(SettingKey.HistoryEnabled)) continue;

			try
			{
				repository.Append(Sample());
				if (DateTimeOffset.Now.Hour == 0 && DateTimeOffset.Now.Minute < every.TotalMinutes)
				{
					Summarise(DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime).AddDays(-1));
					repository.Prune(aiSettings.Integer(AiSettingKey.HistoryRetentionDays));

					int forgotten = memories.Prune();
					if (forgotten > 0) Log.Write("Memory", $"Let go of {forgotten} unused memor{(forgotten == 1 ? "y" : "ies")}");
				}
			}
			catch (Exception ex)
			{
				Log.Error("History", $"Could not record a sample: {ex.Message}");
			}
		}
	}

	/// Everything worth saying about a stretch of the day, in plain lines an LLM can read
	public IReadOnlyList<string> Digest(DateTimeOffset from, DateTimeOffset to)
	{
		double every = Math.Clamp(aiSettings.Integer(AiSettingKey.HistorySampleMinutes), 1, 60);
		var lines = new List<string>();

		foreach (VirtualSensor sensor in sensors.Registered)
		{
			IReadOnlyList<HistoryPoint> points = repository.Read(sensor.Id, from, to);
			if (points.Count < 2) continue;

			if (sensor.Kind == ReadingKind.Boolean)
			{
				double minutes = points.Count(point => point.Value >= 0.5) * every;
				if (minutes > 0) lines.Add($"{sensor.Name.ToLowerInvariant()} for {Spell(minutes)}");
				continue;
			}

			string unit = sensor.Unit;
			lines.Add($"{sensor.Name.ToLowerInvariant()} from {Units.Number(points.Min(point => point.Value))}{unit} " +
				$"to {Units.Number(points.Max(point => point.Value))}{unit}, " +
				$"averaging {Units.Number(points.Average(point => point.Value))}{unit}");
		}

		foreach (VirtualDevice device in sensors.RegisteredDevices)
		{
			IReadOnlyList<HistoryPoint> points = repository.Read(device.Id, from, to);
			double minutes = points.Count(point => point.Value >= 0.5) * every;

			if (minutes > 0) lines.Add($"{device.Name.ToLowerInvariant()} on for {Spell(minutes)}");
		}

		foreach (ActivityCategory category in Enum.GetValues<ActivityCategory>())
		{
			double minutes = repository.Read("activity", from, to)
				.Count(point => Math.Abs(point.Value - (int)category) < 0.5) * every;

			if (minutes > 0) lines.Add($"{category.ToString().ToLowerInvariant()} for {Spell(minutes)}");
		}

		double music = repository.Read("music", from, to).Count(point => point.Value >= 0.5) * every;
		if (music > 0) lines.Add($"music playing for {Spell(music)}");

		return lines;
	}

	/// The day reduced to numbers and kept, because a rhythm needs a series and prose is not one
	public DaySummary Summarise(DateOnly day)
	{
		var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), DateTimeOffset.Now.Offset);
		DateTimeOffset to = from.AddDays(1);
		double every = Math.Clamp(aiSettings.Integer(AiSettingKey.HistorySampleMinutes), 1, 60);

		IReadOnlyList<HistoryPoint> presence = repository.Read(SensorIds.Presence, from, to);
		IReadOnlyList<HistoryPoint> computer = repository.Read(DeviceIds.Computer, from, to);
		IReadOnlyList<HistoryPoint> sleep = repository.Read(SensorIds.Sleep, from, to);
		IReadOnlyList<HistoryPoint> music = repository.Read("music", from, to);

		var byActivity = new Dictionary<string, double>();
		foreach (ActivityCategory category in Enum.GetValues<ActivityCategory>())
		{
			double minutes = repository.Read("activity", from, to)
				.Count(point => Math.Abs(point.Value - (int)category) < 0.5) * every;

			if (minutes > 0) byActivity[category.ToString()] = Math.Round(minutes, 1);
		}

		var byDevice = new Dictionary<string, double>();
		foreach (VirtualDevice device in sensors.RegisteredDevices)
		{
			double minutes = DayRhythm.Minutes(repository.Read(device.Id, from, to), every);
			if (minutes > 0) byDevice[device.Id] = minutes;
		}

		var averages = new Dictionary<string, double>();
		foreach (VirtualSensor sensor in sensors.Registered.Where(entry => entry.Kind == ReadingKind.Number))
		{
			IReadOnlyList<HistoryPoint> points = repository.Read(sensor.Id, from, to);
			if (points.Count > 1) averages[sensor.Id] = Math.Round(points.Average(point => point.Value), 1);
		}

		var summary = new DaySummary(day, day.DayOfWeek,
			DayRhythm.Rose(presence), DayRhythm.Fell(presence),
			DayRhythm.Rose(computer), DayRhythm.Fell(computer),
			DayRhythm.Rose(sleep),
			DayRhythm.Minutes(presence, every),
			DayRhythm.Minutes(computer, every),
			DayRhythm.Minutes(music, every),
			byActivity, byDevice, averages);

		rhythm.Save(summary);
		return summary;
	}

	public IReadOnlyList<DaySummary> Days(int days = 30) => rhythm.Load(days);

	/// Days already on disk were never summarised, so a fresh rhythm can be given its head start
	public int Backfill(int days)
	{
		DateOnly today = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);
		var written = 0;

		for (var back = 1; back <= Math.Clamp(days, 1, 400); back++)
		{
			DateOnly day = today.AddDays(-back);
			var from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), DateTimeOffset.Now.Offset);

			if (repository.Read(SensorIds.Presence, from, from.AddDays(1)).Count < 2
				&& repository.Read(DeviceIds.Computer, from, from.AddDays(1)).Count < 2) continue;

			Summarise(day);
			written++;
		}

		return written;
	}

	/// How today compares with the usual for this weekday, which is what makes a remark worth making
	public RhythmView Rhythm(string metric, int weeks = 8)
	{
		IReadOnlyList<DaySummary> all = rhythm.Load(weeks * 7);
		DateOnly today = DateOnly.FromDateTime(DateTimeOffset.Now.LocalDateTime);

		Func<DaySummary, int?> pick = metric.ToLowerInvariant() switch
		{
			"up" or "firstpresence" => day => day.FirstPresence,
			"bed" or "lastpresence" => day => day.LastPresence,
			"computeron" => day => day.ComputerOn,
			"computeroff" => day => day.ComputerOff,
			_ => day => day.SleepAt
		};

		DaySummary[] sameWeekday = [.. all.Where(day => day.Weekday == today.DayOfWeek && day.Day != today)];
		int? usual = DayRhythm.Usual(sameWeekday.Select(pick));
		int? now = all.FirstOrDefault(day => day.Day == today) is { } current ? pick(current) : null;

		string summary = usual is null
			? $"There is no usual {metric} for a {today.DayOfWeek} yet, only {sameWeekday.Length} of them recorded"
			: now is null
				? $"Usually {DayRhythm.Spell(usual)} on a {today.DayOfWeek}, from {sameWeekday.Length} of them. Nothing today yet"
				: Compare(metric, usual.Value, now.Value, today.DayOfWeek, sameWeekday.Length);

		return new RhythmView(metric, usual, now, sameWeekday.Length, summary);
	}

	private static string Compare(string metric, int usual, int now, DayOfWeek weekday, int days)
	{
		int drift = now - usual;
		string usually = $"usually {DayRhythm.Spell(usual)} on a {weekday}, from {days} of them";

		return Math.Abs(drift) switch
		{
			< 20 => $"{metric} at {DayRhythm.Spell(now)}, about normal — {usually}",
			< 60 => $"{metric} at {DayRhythm.Spell(now)}, {Math.Abs(drift)} minutes {(drift > 0 ? "later" : "earlier")} than usual — {usually}",
			_ => $"{metric} at {DayRhythm.Spell(now)}, {Math.Abs(drift) / 60.0:0.#} hours {(drift > 0 ? "later" : "earlier")} than usual — {usually}"
		};
	}

	private static string Spell(double minutes) =>
		minutes >= 90 ? $"{minutes / 60:0.#} hours" : $"{minutes:0} minutes";

	public Result<BaselineResult> CompareToUsual(string metric, int days = 21, DateTimeOffset? at = null)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		if (!repository.Metrics.Contains(wanted))
			return Result.Fail<BaselineResult>($"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}");

		DateTimeOffset moment = at ?? DateTimeOffset.Now;
		int window = Math.Clamp(days, 1, 365);

		IReadOnlyList<HistoryPoint> points = repository.Read(wanted, moment.AddDays(-window), moment);
		double? current = points.Count > 0 ? points[^1].Value : null;

		return Result.Ok(HistoryBaseline.Build(wanted, Unit(wanted), points, current, moment.Hour));
	}

	public Result<CorrelationResult> Correlate(string metric, string against, int hours = 24)
	{
		if (Resolve(metric) is not { } left) return Result.Fail<CorrelationResult>(Unknown(metric));
		if (Resolve(against) is not { } right) return Result.Fail<CorrelationResult>(Unknown(against));

		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset from = now.AddHours(-Math.Clamp(hours, 1, 720));

		return Result.Ok(HistoryCorrelation.Correlate(left, right,
			repository.Read(left, from, now), repository.Read(right, from, now)));
	}

	public Result<CorrelationResult> DuringActivity(string metric, ActivityCategory category, int hours = 72)
	{
		if (Resolve(metric) is not { } wanted) return Result.Fail<CorrelationResult>(Unknown(metric));

		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset from = now.AddHours(-Math.Clamp(hours, 1, 720));

		return Result.Ok(HistoryCorrelation.Split(wanted, Unit(wanted), "activity", (int)category,
			repository.Read(wanted, from, now), repository.Read("activity", from, now),
			category.ToString().ToLowerInvariant()));
	}

	/// How a room metric has moved since the current stretch of desktop activity began
	public Result<SessionInsight> ThisSession(string metric, int hours = 12)
	{
		if (Resolve(metric) is not { } wanted) return Result.Fail<SessionInsight>(Unknown(metric));

		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset from = now.AddHours(-Math.Clamp(hours, 1, 720));

		IReadOnlyList<HistoryPoint> activity = repository.Read("activity", from, now);
		if (activity.Count == 0) return Result.Fail<SessionInsight>("Nothing has been recorded about the desktop yet");

		var category = (ActivityCategory)(int)activity[^1].Value;
		DateTimeOffset since = activity[^1].At;

		for (int i = activity.Count - 1; i >= 0; i--)
		{
			if ((int)activity[i].Value != (int)category) break;
			since = activity[i].At;
		}

		return Result.Ok(HistoryCorrelation.Session(wanted, Unit(wanted), category, since, repository.Read(wanted, from, now)));
	}

	/// A metric is a sensor, a device or one of the desktop facts, and only sensors carry a unit
	private string Unit(string metric) => sensors.Sensor(metric)?.Unit ?? "";

	private string? Resolve(string metric)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		return repository.Metrics.Contains(wanted) ? wanted : null;
	}

	private string Unknown(string metric) =>
		$"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}";

	private HistorySample Sample()
	{
		var values = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

		foreach (VirtualSensor sensor in sensors.Registered)
			values[sensor.Id] = sensors.Read(sensor.Id)?.Value;

		foreach (VirtualDevice device in sensors.RegisteredDevices)
			values[device.Id] = devices.State(device.Id) == PowerState.On ? 1 : 0;

		values["activity"] = activity.Current is { } doing ? (int)doing.Category : null;
		values["music"] = activity.Current?.Playing is { } playing ? playing.Paused ? 0 : 1 : null;

		return new HistorySample(DateTimeOffset.Now, values);
	}

	private static double Percent(double used, double total) => total > 0 ? Math.Round(used / total * 100, 1) : 0;

	private static IReadOnlyList<HistoryPoint> Thin(IReadOnlyList<HistoryPoint> points)
	{
		if (points.Count <= MaxSamples) return points;

		int step = (int)Math.Ceiling(points.Count / (double)MaxSamples);
		return [.. points.Where((_, index) => index % step == 0)];
	}
}
