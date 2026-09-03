namespace CortanaLib.Contracts;

/// Sample pushed by the desktop agent, and produced locally by the Kernel for the Raspberry
public sealed record MachineSample(
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
	double GpuPower = 0);
