using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Services;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed record UserDeviceActionPerformed(string Device, SwitchAction Action, CommandOrigin Origin, DateTimeOffset At) : IDomainEvent;

/// The one place device commands are executed
public sealed class DeviceService(
	Fabric devices,
	AutomationEngine automation,
	IEnumerable<IChannelWriter> writers,
	IComputerEndpoint computer,
	IHostMachine host,
	SettingsStore settings,
	NotificationService notifications,
	ActivityRegistry activity,
	IEventBus bus)
{
	private readonly Lock _switchGate = new();
	private readonly Dictionary<string, (VirtualDevice Device, CommandOrigin Origin)> _pendingOff = new(StringComparer.OrdinalIgnoreCase);
	private CancellationTokenSource? _shutdown;

	public IReadOnlyList<DeviceView> All() => devices.Devices(automation.OverrideUntil);

	public PowerState State(string device) => devices.State(device);

	public DeviceView? Describe(string device) =>
		devices.Device(device) is { } registered
			? All().FirstOrDefault(view => view.Device.Equals(registered.Id, StringComparison.OrdinalIgnoreCase))
			: null;

	public bool Known(string device) => devices.Device(device) is not null;

	/// The id behind whatever the caller said, so the rest of the pipeline only ever sees ids
	public string Resolve(string device) => devices.Device(device)?.Id ?? device;

	/// The desktop, as the registrations declare it rather than by a fixed name
	public DeviceView? Machine => devices.Machine is { } machine ? Describe(machine.Id) : null;

	private IChannelWriter? Writer(ChannelRef channel) =>
		writers.FirstOrDefault(writer => writer.Handles(channel.Source) && writer.Controls(channel.Channel));

	public Result<string> Switch(string wanted, SwitchAction action, CommandOrigin origin)
	{
		string device = Resolve(wanted);
		SwitchAction resolved = devices.Resolve(device, action);

		Result<string> result = device switch
		{
			_ when device.Equals(devices.Machine?.Id, StringComparison.OrdinalIgnoreCase) => SwitchComputer(resolved),
			_ when device.Equals(Supply, StringComparison.OrdinalIgnoreCase) => SwitchPower(resolved, origin),
			_ => Apply(device, resolved == SwitchAction.On ? PowerState.On : PowerState.Off, origin)
		};

		if (result.IsOk && origin.IsUser)
			bus.Publish(new UserDeviceActionPerformed(device, resolved, origin, DateTimeOffset.Now));

		return result;
	}

	public Result<string> ApplyAutomatic(string device, PowerState state, string reason) =>
		Apply(device, state, CommandOrigin.Automation with { Reason = reason });

	/// An output cannot be read back, so a restart writes the last known state onto the hardware again
	public void Restore()
	{
		foreach ((string key, PowerState state) in devices.Written)
		{
			string[] parts = key.Split('/');
			if (parts.Length != 2) continue;

			var channel = new ChannelRef(parts[0], parts[1]);
			if (Writer(channel) is not { } writer) continue;

			writer.Apply(channel.Channel, state, false);
			Log.Write("Devices", $"Restored {key} to {state.ToString().ToLowerInvariant()}");
		}
	}

	private Result<string> Apply(string device, PowerState state, CommandOrigin origin)
	{
		lock (_switchGate)
		{
			if (devices.Device(device) is not { } registered) return Result.Fail<string>($"'{device}' is not registered");

			if (state == PowerState.On && _pendingOff.Values.Any(pending => Overlaps(registered, pending.Device)))
				CancelShutdown();

			// Protect the physical supply even when reached through a group or another channel alias.
			// A disconnect during the grace period must not let a second request cut power early.
			if (state == PowerState.Off && Supply is { } supply && devices.Device(supply) is { } power
				&& Overlaps(registered, power) && (computer.Connected || _shutdown is not null))
				return QueuePowerOff(registered, origin);

			return Write(registered, state, origin);
		}
	}

	private bool Overlaps(VirtualDevice first, VirtualDevice second)
	{
		HashSet<string> channels = PhysicalChannels(first).ToHashSet(StringComparer.OrdinalIgnoreCase);
		return PhysicalChannels(second).Any(channels.Contains);
	}

	private IEnumerable<string> PhysicalChannels(VirtualDevice device) => device.Channels.SelectMany(channel =>
		(Writer(channel)?.Linked(channel.Channel) ?? [channel.Channel]).Select(linked => $"{channel.Source}/{linked}"));

	private Result<string> Write(VirtualDevice registered, PowerState state, CommandOrigin origin)
	{
		(ChannelRef Channel, IChannelWriter Writer)[] channels =
		[
			.. registered.Channels
				.Select(channel => (Channel: channel, Writer: Writer(channel)))
				.Where(entry => entry.Writer is not null)
				.Select(entry => (entry.Channel, Writer: entry.Writer!))
		];

		if (channels.Length == 0) return Result.Fail<string>($"{registered.Name} has no output anything can drive");

		foreach ((ChannelRef channel, IChannelWriter writer) in channels)
		{
			Result<string> written = writer.Apply(channel.Channel, state, registered.Pulse);
			if (!written.IsOk) return written;
		}

		ChannelRef[] moved =
		[
			.. channels.SelectMany(entry => entry.Writer.Linked(entry.Channel.Channel)
				.Select(linked => entry.Channel with { Channel = linked }))
		];

		Log.Write("Devices", $"{registered.Name} {state.ToString().ToLowerInvariant()} " +
			$"({string.Join(", ", channels.Select(entry => entry.Channel.Channel))}) by {origin.Actor} via {origin.Surface}" +
			$"{(origin.Reason.Length > 0 ? $": {origin.Reason}" : "")}");

		foreach ((string touched, PowerState now) in devices.SetChannels(moved, state))
			bus.Publish(new DeviceStateChanged(touched, now, origin, DateTimeOffset.Now));

		return Result.Ok(state.ToString());
	}

	/// The device that feeds the computer, as its registration declares it
	private string? Supply => devices.Machine?.PoweredBy;

	private Result<string> SwitchComputer(SwitchAction action)
	{
		if (action == SwitchAction.On)
		{
			lock (_switchGate) CancelShutdown();
			if (Supply is { } supplied && devices.State(supplied) == PowerState.Off)
			{
				Result<string> supply = Apply(supplied, PowerState.On, CommandOrigin.Internal);
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

	private Result<string> SwitchPower(SwitchAction action, CommandOrigin origin)
	{
		if (action == SwitchAction.On) return SwitchComputer(SwitchAction.On);

		if (Supply is not { } supply) return Result.Fail<string>("The computer declares no supply to cut");
		return Apply(supply, PowerState.Off, origin);
	}

	/// Called under the switch lock. Concurrent requests share one shutdown and retain their targets.
	private Result<string> QueuePowerOff(VirtualDevice device, CommandOrigin origin)
	{
		_pendingOff[device.Id] = (device, origin);
		if (_shutdown is not null) return Result.Ok($"Waiting for the computer to shut down before switching {device.Name} off");

		var shutdown = new CancellationTokenSource();
		_shutdown = shutdown;
		_ = Task.Run(async () =>
		{
			try
			{
				Result<string> sent = await computer.Send(ComputerCommand.Shutdown, "", shutdown.Token);
				if (!sent.IsOk) throw new InvalidOperationException(sent.Error);
				await computer.WaitUntilPoweredOff(settings.Seconds(SettingKey.ComputerShutdownGraceSeconds), shutdown.Token);

				string[] names;
				lock (_switchGate)
				{
					if (shutdown.IsCancellationRequested || !ReferenceEquals(_shutdown, shutdown)) return;
					if (computer.Connected) throw new InvalidOperationException("The computer is still connected");

					names = [.. _pendingOff.Values.Select(pending => pending.Device.Name)];
					foreach (var pending in _pendingOff.Values.ToArray())
					{
						Result<string> applied = Write(pending.Device, PowerState.Off, pending.Origin);
						if (!applied.IsOk) throw new InvalidOperationException(applied.Error);
					}
					// Complete under the same lock, so a new request cannot join a finished batch.
					_shutdown = null;
					_pendingOff.Clear();
				}

				notifications.Raise(NotificationSource.Computer, $"Computer off; {string.Join(", ", names)} switched off",
					reason: "the desktop stopped responding and the shutdown grace period elapsed");
			}
			catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
			{
				// A newer On command wins. It cannot undo an OS shutdown already sent, but keeps power on.
			}
			catch (Exception ex)
			{
				Log.Error("Devices", $"The shutdown sequence failed: {ex.Message}");
				notifications.Raise(NotificationSource.Computer, "Could not finish switching the requested devices off",
					NotificationLevel.Warning, ex.Message);
			}
			finally
			{
				lock (_switchGate)
				{
					if (ReferenceEquals(_shutdown, shutdown))
					{
						_shutdown = null;
						_pendingOff.Clear();
					}
				}
				shutdown.Dispose();
			}
		});

		return Result.Ok($"Shutting the computer down before switching {device.Name} off");
	}

	private void CancelShutdown()
	{
		_shutdown?.Cancel();
		_shutdown = null;
		_pendingOff.Clear();
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
			bus.Publish(new UserDeviceActionPerformed(devices.Machine?.Id ?? DeviceIds.Computer, SwitchAction.Toggle, origin, DateTimeOffset.Now));

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
