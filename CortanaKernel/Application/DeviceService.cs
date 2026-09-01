using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Devices;
using CortanaKernel.Domain.Services;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed record UserDeviceActionPerformed(DeviceId Device, SwitchAction Action, CommandOrigin Origin, DateTimeOffset At) : IDomainEvent;

/// The one place device commands are executed
public sealed class DeviceService(
	DeviceRegistry devices,
	AutomationEngine automation,
	ILocalDeviceController controller,
	IComputerEndpoint computer,
	IHostMachine host,
	SettingsStore settings,
	NotificationService notifications,
	ActivityRegistry activity,
	IEventBus bus)
{
	public IReadOnlyList<DeviceView> All() => devices.All(automation.OverrideUntil);

	public PowerState State(DeviceId device) => devices.State(device);

	public Result<string> Switch(DeviceId device, SwitchAction action, CommandOrigin origin)
	{
		SwitchAction resolved = devices.Resolve(device, action);

		Result<string> result = device switch
		{
			DeviceId.Computer => SwitchComputer(resolved),
			DeviceId.Power => SwitchPower(resolved),
			_ => Apply(device, resolved == SwitchAction.On ? PowerState.On : PowerState.Off, origin)
		};

		if (result.IsOk && origin.IsUser)
			bus.Publish(new UserDeviceActionPerformed(device, resolved, origin, DateTimeOffset.Now));

		return result;
	}

	public Result<string> SwitchRoom(SwitchAction action, CommandOrigin origin)
	{
		bool on = devices.Resolve(DeviceId.Power, action) == SwitchAction.On;

		if (!on)
		{
			Switch(DeviceId.Lamp, SwitchAction.Off, origin);
			Result<string> power = Switch(DeviceId.Power, SwitchAction.Off, origin);
			return power.IsOk ? Result.Ok("Room off") : power;
		}

		Result<string> supply = Switch(DeviceId.Power, SwitchAction.On, origin);
		if (!supply.IsOk) return supply;

		if (!settings.Flag(SettingKey.AutomationEnabled)) Switch(DeviceId.Lamp, SwitchAction.On, origin);

		return Result.Ok(settings.Flag(SettingKey.AutomationEnabled)
			? "Room on, the lamp is left to automation"
			: "Room on");
	}

	public Result<string> ApplyAutomatic(DeviceId device, PowerState state, string reason) =>
		Apply(device, state, CommandOrigin.Automation with { Reason = reason });

	private Result<string> Apply(DeviceId device, PowerState state, CommandOrigin origin)
	{
		if (!controller.Controls(device)) return Result.Fail<string>($"{device} has no local output");

		Result<string> written = controller.Apply(device, state);
		if (!written.IsOk) return written;

		foreach (DeviceId linked in controller.Linked(device))
			if (devices.Set(linked, state))
				bus.Publish(new DeviceStateChanged(linked, state, origin, DateTimeOffset.Now));

		return Result.Ok(state.ToString());
	}

	private Result<string> SwitchComputer(SwitchAction action)
	{
		if (action == SwitchAction.On)
		{
			if (devices.State(DeviceId.Power) == PowerState.Off)
			{
				Result<string> supply = Apply(DeviceId.Power, PowerState.On, CommandOrigin.Internal);
				if (!supply.IsOk) return supply;
			}

			if (computer.Connected) return Result.Ok("The computer is already on");

			Result<string> wake = computer.WakeOnLan();
			return wake.IsOk ? Result.Ok("Waking the computer") : wake;
		}

		if (!computer.Connected) return Result.Ok("The computer is already off");

		_ = computer.Send(ComputerCommand.Shutdown, "");
		return Result.Ok("Shutting the computer down");
	}

	private Result<string> SwitchPower(SwitchAction action)
	{
		if (action == SwitchAction.On) return SwitchComputer(SwitchAction.On);

		if (!computer.Connected) return Apply(DeviceId.Power, PowerState.Off, CommandOrigin.Internal);

		_ = Task.Run(async () =>
		{
			try
			{
				await computer.Send(ComputerCommand.Shutdown, "");
				await computer.WaitUntilPoweredOff(settings.Seconds(SettingKey.ComputerShutdownGraceSeconds));
				Apply(DeviceId.Power, PowerState.Off, CommandOrigin.Internal);
				notifications.Raise(NotificationSource.Computer, "Computer off, power cut",
					reason: $"the desktop went quiet, then a {settings.Seconds(SettingKey.ComputerShutdownGraceSeconds).TotalSeconds:0}s grace period elapsed");
			}
			catch (Exception ex)
			{
				Log.Error("Devices", $"The shutdown sequence failed: {ex.Message}");
			}
		});

		return Result.Ok("Shutting the computer down, power will be cut once it is off");
	}

	// ---------- the desktop machine ----------

	public bool ComputerConnected => computer.Connected;

	public async Task<Result<string>> CommandComputer(ComputerCommand command, string argument, CommandOrigin origin, CancellationToken token = default)
	{
		if (!computer.Connected) return Result.Fail<string>("The computer is not connected");

		if (command == ComputerCommand.Notify && !origin.IsUser && ActivityRules.DoNotDisturb(activity.Current))
			return Result.Ok("Held back, the desktop is busy");

		Result<string> result = await computer.Send(command, argument, token);

		if (result.IsOk && origin.IsUser)
			bus.Publish(new UserDeviceActionPerformed(DeviceId.Computer, SwitchAction.Toggle, origin, DateTimeOffset.Now));

		return result;
	}

	// ---------- the Raspberry ----------

	public Task<Result<string>> CommandRaspberry(RaspberryCommand command, string argument, CancellationToken token = default) => command switch
	{
		RaspberryCommand.Shutdown => Task.FromResult(host.PowerOff()),
		RaspberryCommand.Reboot => Task.FromResult(host.Reboot()),
		RaspberryCommand.RunShellCommand => host.RunShellCommand(argument, token),
		_ => Task.FromResult(Result.Fail<string>($"Unknown command '{command}'"))
	};

	public async Task<IReadOnlyList<RaspberryInfoView>> HostInformation(CancellationToken token = default) =>
	[
		new(RaspberryInfo.Temperature, Units.Number(host.CpuTemperature), Units.For(RaspberryInfo.Temperature)),
		new(RaspberryInfo.Location, host.Location.ToString(), ""),
		new(RaspberryInfo.Gateway, host.Gateway, ""),
		new(RaspberryInfo.PublicIp, await host.PublicIpAddress(token), "")
	];

	public async Task<Result<string>> HostInformation(RaspberryInfo info, CancellationToken token = default) => info switch
	{
		RaspberryInfo.Temperature => Result.Ok(Units.Number(host.CpuTemperature)),
		RaspberryInfo.Location => Result.Ok(host.Location.ToString()),
		RaspberryInfo.Gateway => Result.Ok(host.Gateway),
		RaspberryInfo.PublicIp => Result.Ok(await host.PublicIpAddress(token)),
		_ => Result.Fail<string>($"Unknown property '{info}'")
	};
}
