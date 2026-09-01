using CortanaLib.Contracts;

namespace CortanaKernel.Domain.Metrics;

/// Latest machine samples
public sealed class MetricsRegistry
{
	private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(2);

	private readonly Lock _gate = new();

	private MachineSample? _computer;
	private DateTimeOffset _computerAt;
	private MachineSample _raspberry = new("", "", 0, 0, 0, 0, 0, 0, 0, 0, 0);
	private DateTimeOffset _raspberryAt;

	public void StoreComputer(MachineSample sample, DateTimeOffset now)
	{
		lock (_gate)
		{
			_computer = sample;
			_computerAt = now;
		}
	}

	public void StoreRaspberry(MachineSample sample, DateTimeOffset now)
	{
		lock (_gate)
		{
			_raspberry = sample;
			_raspberryAt = now;
		}
	}

	public MetricsView? Computer(DateTimeOffset now)
	{
		lock (_gate) return _computer == null ? null : View(_computer, _computerAt, now - _computerAt > StaleAfter);
	}

	public MetricsView Raspberry(DateTimeOffset now)
	{
		lock (_gate) return View(_raspberry, _raspberryAt == default ? now : _raspberryAt, false);
	}

	private static MetricsView View(MachineSample sample, DateTimeOffset at, bool stale) =>
		new(sample.Host, sample.Os, sample.CpuLoad, sample.CpuTemp, sample.MemoryUsed, sample.MemoryTotal,
			sample.GpuLoad, sample.GpuTemp, sample.DiskUsed, sample.DiskTotal, sample.Uptime, at, stale);
}
