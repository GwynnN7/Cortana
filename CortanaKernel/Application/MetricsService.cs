using CortanaKernel.Domain.Metrics;
using CortanaLib.Contracts;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// Samples the Raspberry locally and accepts the desktop's pushed samples
public sealed class MetricsService(MetricsRegistry registry, StateBroadcaster broadcaster) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

	public MetricsView? Computer() => registry.Computer(DateTimeOffset.Now);

	public MetricsView Raspberry() => registry.Raspberry(DateTimeOffset.Now);

	public void StoreComputer(MachineSample sample)
	{
		registry.StoreComputer(sample, DateTimeOffset.Now);
		broadcaster.Touch();
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		registry.StoreRaspberry(MachineMetrics.Collect(), DateTimeOffset.Now);

		using var timer = new PeriodicTimer(Interval);
		while (await timer.WaitForNextTickAsync(stoppingToken))
			registry.StoreRaspberry(MachineMetrics.Collect(), DateTimeOffset.Now);
	}
}
