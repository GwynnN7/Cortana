using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Devices;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

public interface IComputerPresence
{
	void Changed(bool connected);
}

public sealed class ComputerPresenceService(
	DeviceRegistry devices,
	NotificationService notifications,
	IEventBus bus) : IComputerPresence
{
	public void Changed(bool connected)
	{
		PowerState state = connected ? PowerState.On : PowerState.Off;
		if (!devices.Set(DeviceId.Computer, state)) return;

		bus.Publish(new DeviceStateChanged(DeviceId.Computer, state, CommandOrigin.Internal, DateTimeOffset.Now));

		// If the desktop is talking to us its mains supply is obviously live
		if (connected && devices.Set(DeviceId.Power, PowerState.On))
			bus.Publish(new DeviceStateChanged(DeviceId.Power, PowerState.On, CommandOrigin.Internal, DateTimeOffset.Now));

		notifications.Raise(NotificationSource.Computer, connected ? "Computer online" : "Computer offline");
		bus.Publish(new ComputerConnectionChanged(connected, DateTimeOffset.Now));
	}
}
