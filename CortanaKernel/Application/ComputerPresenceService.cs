using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaKernel.Domain.Fabric;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public interface IComputerPresence
{
	void Changed(bool connected);
	void ActivityChanged(DesktopActivity activity);
	void Observed(IReadOnlyDictionary<string, double> values);
	void Described(IReadOnlyDictionary<string, string> facts);
}

public sealed class ComputerPresenceService(
	Fabric devices,
	ActivityRegistry activity,
	Fabric fabric,
	Lazy<SensorService> sensors,
	NotificationService notifications,
	IEventBus bus) : IComputerPresence
{
	public void Observed(IReadOnlyDictionary<string, double> values) =>
		sensors.Value.Observe(SourceIds.Computer, values, DateTimeOffset.Now);

	public void Described(IReadOnlyDictionary<string, string> facts) => fabric.Describe(SourceIds.Computer, facts);

	public void ActivityChanged(DesktopActivity update)
	{
		if (activity.Set(update)) bus.Publish(new DesktopActivityChanged(update, DateTimeOffset.Now));
	}

	public void Changed(bool connected)
	{
		if (devices.Machine is not { } machine) return;

		PowerState state = connected ? PowerState.On : PowerState.Off;
		if (!devices.Set(machine.Id, state)) return;

		bus.Publish(new DeviceStateChanged(machine.Id, state, CommandOrigin.Internal, DateTimeOffset.Now));

		// If the desktop is talking to us its mains supply is obviously live
		if (connected && machine.PoweredBy is { } supply && devices.Set(supply, PowerState.On))
			bus.Publish(new DeviceStateChanged(supply, PowerState.On, CommandOrigin.Internal, DateTimeOffset.Now));

		if (connected) fabric.Touch(SourceIds.Computer);
		else
		{
			fabric.Dropped(SourceIds.Computer);
			activity.Clear();
		}

		notifications.Raise(NotificationSource.Computer, connected ? "Computer Online" : "Computer Offline",
			reason: connected ? "the desktop agent connected" : "the desktop agent disconnected");
		bus.Publish(new ComputerConnectionChanged(connected, DateTimeOffset.Now));
	}
}
