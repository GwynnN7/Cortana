using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.History;
using CortanaKernel.Domain.Metrics;
using CortanaKernel.Domain.Sensors;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed class HistoryService(
	IHistoryRepository repository,
	SensorRegistry sensors,
	DeviceService devices,
	MetricsRegistry metrics,
	ActivityRegistry activity,
	MemoryStore memories,
	AiSettingsStore aiSettings) : BackgroundService
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
			return Result.Ok(new HistorySeries(wanted, Units.ForMetric(wanted), 0, 0, 0, 0, from, to, []));

		return Result.Ok(new HistorySeries(
			wanted,
			Units.ForMetric(wanted),
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
			request.Function, metric, points, comparison, request.At, request.State, request.WindowMinutes));
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

			try
			{
				repository.Append(Sample());
				if (DateTimeOffset.Now.Hour == 0 && DateTimeOffset.Now.Minute < every.TotalMinutes)
				{
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

	public Result<BaselineResult> CompareToUsual(string metric, int days = 21, DateTimeOffset? at = null)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		if (!repository.Metrics.Contains(wanted))
			return Result.Fail<BaselineResult>($"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}");

		DateTimeOffset moment = at ?? DateTimeOffset.Now;
		int window = Math.Clamp(days, 1, 365);

		IReadOnlyList<HistoryPoint> points = repository.Read(wanted, moment.AddDays(-window), moment);
		double? current = points.Count > 0 ? points[^1].Value : null;

		return Result.Ok(HistoryBaseline.Build(wanted, points, current, moment.Hour));
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

		return Result.Ok(HistoryCorrelation.Split(wanted, "activity", (int)category,
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

		return Result.Ok(HistoryCorrelation.Session(wanted, category, since, repository.Read(wanted, from, now)));
	}

	private string? Resolve(string metric)
	{
		string wanted = metric.Trim().ToLowerInvariant();
		return repository.Metrics.Contains(wanted) ? wanted : null;
	}

	private string Unknown(string metric) =>
		$"Unknown metric '{metric}'. Valid metrics: {string.Join(", ", repository.Metrics)}";

	private HistorySample Sample()
	{
		SensorReading? reading = sensors.Last;
		MetricsView pi = metrics.Raspberry(DateTimeOffset.Now);
		MetricsView? pc = metrics.Computer(DateTimeOffset.Now) is { Stale: false } fresh ? fresh : null;

		var values = new Dictionary<string, double?>
		{
			["temperature"] = reading?.Temperature,
			["humidity"] = reading?.Humidity,
			["light"] = reading?.Light,
			["co2"] = reading?.Co2,
			["tvoc"] = reading?.Tvoc,
			["motion"] = reading == null ? null : reading.Motion ? 1 : 0,
			["lamp"] = devices.State(DeviceId.Lamp) == PowerState.On ? 1 : 0,
			["computer"] = devices.State(DeviceId.Computer) == PowerState.On ? 1 : 0,
			["pi_cpu"] = pi.CpuLoad,
			["pi_temp"] = pi.CpuTemp,
			["pi_ram"] = Percent(pi.MemoryUsed, pi.MemoryTotal),
			["pc_cpu"] = pc?.CpuLoad,
			["pc_temp"] = pc?.CpuTemp,
			["pc_ram"] = pc == null ? null : Percent(pc.MemoryUsed, pc.MemoryTotal),
			["pc_gpu"] = pc?.GpuLoad,
			["pc_gpu_temp"] = pc?.GpuTemp,
			["activity"] = activity.Current is { } doing ? (int)doing.Category : null,
			["music"] = activity.Current?.Playing is { } playing ? playing.Paused ? 0 : 1 : null
		};

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
