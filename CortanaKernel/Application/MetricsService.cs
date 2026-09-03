using CortanaKernel.Domain.Fabric;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// Samples the Raspberry locally and turns any machine sample into readings plus facts
public sealed class MetricsService(Fabric fabric) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

	/// The live numbers are sensors; the slow ones describe the machine
	public static Dictionary<string, double> Readings(MachineSample sample) => new()
	{
		["cpu"] = Math.Round(sample.CpuLoad, 1),
		["cpu_temp"] = Math.Round(sample.CpuTemp, 1),
		["ram"] = sample.MemoryTotal > 0 ? Math.Round(sample.MemoryUsed / sample.MemoryTotal * 100, 1) : 0,
		["disk"] = sample.DiskTotal > 0 ? Math.Round(sample.DiskUsed / sample.DiskTotal * 100, 1) : 0
	};

	public static Dictionary<string, string> Facts(MachineSample sample) => new()
	{
		["name"] = sample.Host,
		["os"] = sample.Os,
		["memory"] = $"{sample.MemoryUsed:F1}/{sample.MemoryTotal:F1} GB",
		["disk"] = $"{sample.DiskUsed:F0}/{sample.DiskTotal:F0} GB",
		["uptime"] = Units.Elapsed(TimeSpan.FromSeconds(sample.Uptime))
	};

	private void Publish(MachineSample sample)
	{
		fabric.Describe(SourceIds.Raspberry, Facts(sample));
		fabric.Observe(SourceIds.Raspberry, Readings(sample), DateTimeOffset.Now);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		Publish(MachineMetrics.Collect());

		using var timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
			Publish(MachineMetrics.Collect());
	}
}
