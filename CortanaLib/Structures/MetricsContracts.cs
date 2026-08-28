namespace CortanaLib.Structures;

public record PostMetrics(
	string Host,
	string Os,
	double CpuLoad,
	double CpuTemp,
	double MemoryUsed,
	double MemoryTotal,
	double GpuLoad,
	double GpuTemp,
	double DiskUsed,
	double DiskTotal,
	long Uptime);

public record MetricsResponse(
	string Host,
	string Os,
	double CpuLoad,
	double CpuTemp,
	double MemoryUsed,
	double MemoryTotal,
	double GpuLoad,
	double GpuTemp,
	double DiskUsed,
	double DiskTotal,
	long Uptime,
	DateTime Timestamp,
	bool Stale) : IApiResponse;
