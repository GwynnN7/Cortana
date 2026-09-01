using CortanaKernel.Domain.Metrics;
using CortanaLib.Contracts;

namespace CortanaKernel.Application;

/// Builds the read model
public sealed class SnapshotService(
	DeviceService devices,
	SensorService sensors,
	AutomationService automation,
	SettingsService settings,
	ServiceControlService services,
	MetricsRegistry metrics)
{
	public async Task<CortanaSnapshot> Build(CancellationToken token = default) => new(
		DateTimeOffset.Now,
		devices.All(),
		sensors.All(),
		automation.View(),
		settings.All(),
		await devices.HostInformation(token),
		await services.All(token),
		metrics.Computer(DateTimeOffset.Now),
		metrics.Raspberry(DateTimeOffset.Now));

	public AutomationDiagnostics Diagnostics(IReadOnlyList<NotificationEntry> recent) => new(
		automation.View(),
		automation.Engine.LastDecision,
		devices.All(),
		sensors.All(),
		settings.All(),
		automation.Engine.Decisions,
		recent);
}
