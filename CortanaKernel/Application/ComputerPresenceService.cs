using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaKernel.Domain.Devices;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public interface IComputerPresence
{
	void Changed(bool connected);
	void ActivityChanged(DesktopActivity activity);
}

public sealed class ComputerPresenceService(
	DeviceRegistry devices,
	ActivityRegistry activity,
	NotificationService notifications,
	IEventBus bus) : IComputerPresence
{
	public void ActivityChanged(DesktopActivity update)
	{
		if (activity.Set(update)) bus.Publish(new DesktopActivityChanged(update, DateTimeOffset.Now));
	}

	public void Changed(bool connected)
	{
		PowerState state = connected ? PowerState.On : PowerState.Off;
		if (!devices.Set(DeviceId.Computer, state)) return;

		bus.Publish(new DeviceStateChanged(DeviceId.Computer, state, CommandOrigin.Internal, DateTimeOffset.Now));

		// If the desktop is talking to us its mains supply is obviously live
		if (connected && devices.Set(DeviceId.Power, PowerState.On))
			bus.Publish(new DeviceStateChanged(DeviceId.Power, PowerState.On, CommandOrigin.Internal, DateTimeOffset.Now));

		if (!connected) activity.Clear();

		notifications.Raise(NotificationSource.Computer, connected ? "Computer Online" : "Computer Offline",
			reason: connected ? "the desktop agent connected" : "the desktop agent disconnected");
		bus.Publish(new ComputerConnectionChanged(connected, DateTimeOffset.Now));
	}
}
