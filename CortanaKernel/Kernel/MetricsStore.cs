using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class MetricsStore
{
	private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);
	private static readonly Lock Gate = new();

	private static PostMetrics? _latest;
	private static DateTime _timestamp;

	public static void Store(PostMetrics metrics)
	{
		lock (Gate)
		{
			_latest = metrics;
			_timestamp = DateTime.Now;
		}
	}

	private static readonly TimeSpan LocalInterval = TimeSpan.FromSeconds(10);
	private static System.Threading.Timer? _localTimer;
	private static PostMetrics _local = SystemMonitor.Collect();

	public static void StartLocalSampler()
	{
		_localTimer?.Dispose();
		_localTimer = new System.Threading.Timer(_ => _local = SystemMonitor.Collect(), null, LocalInterval, LocalInterval);
	}

	public static MetricsResponse Local()
	{
		PostMetrics metrics = _local;

		return new MetricsResponse(
			metrics.Host, metrics.Os, metrics.CpuLoad, metrics.CpuTemp,
			metrics.MemoryUsed, metrics.MemoryTotal, metrics.GpuLoad, metrics.GpuTemp,
			metrics.DiskUsed, metrics.DiskTotal, metrics.Uptime, DateTime.Now, false);
	}

	public static IOption<MetricsResponse> Latest()
	{
		lock (Gate)
		{
			if (_latest is null) return new None<MetricsResponse>();

			return new Some<MetricsResponse>(new MetricsResponse(
				_latest.Host, _latest.Os, _latest.CpuLoad, _latest.CpuTemp,
				_latest.MemoryUsed, _latest.MemoryTotal, _latest.GpuLoad, _latest.GpuTemp,
				_latest.DiskUsed, _latest.DiskTotal, _latest.Uptime,
				_timestamp, DateTime.Now - _timestamp > StaleAfter));
		}
	}

	public static string Render(MetricsResponse metrics)
	{
		var lines = new List<string>
		{
			$"{metrics.Host} ({metrics.Os})",
			$"CPU: {metrics.CpuLoad:F0}%{(metrics.CpuTemp > 0 ? $" - {metrics.CpuTemp:F0}°C" : "")}",
			$"RAM: {metrics.MemoryUsed:F1}/{metrics.MemoryTotal:F1} GB"
		};

		if (metrics.GpuTemp > 0 || metrics.GpuLoad > 0)
			lines.Add($"GPU: {metrics.GpuLoad:F0}%{(metrics.GpuTemp > 0 ? $" - {metrics.GpuTemp:F0}°C" : "")}");

		lines.Add($"Disk: {metrics.DiskUsed:F0}/{metrics.DiskTotal:F0} GB");
		lines.Add($"Uptime: {TimeSpan.FromSeconds(metrics.Uptime):d\\d\\ hh\\:mm}");

		if (metrics.Stale) lines.Add($"(stale, last seen {metrics.Timestamp:HH:mm})");

		return string.Join("\n", lines);
	}
}
