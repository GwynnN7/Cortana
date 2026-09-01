using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Devices;

/// GPIO outputs cannot be read back, so this is the interal belief about each device
public sealed class DeviceRegistry
{
	private readonly Dictionary<DeviceId, PowerState> _states =
		Enum.GetValues<DeviceId>().ToDictionary(device => device, _ => PowerState.Off);

	private readonly Lock _gate = new();

	public PowerState State(DeviceId device)
	{
		lock (_gate) return _states[device];
	}

	public bool IsOn(DeviceId device) => State(device) == PowerState.On;

	/// Returns true when the state actually moved
	public bool Set(DeviceId device, PowerState state)
	{
		lock (_gate)
		{
			if (_states[device] == state) return false;
			_states[device] = state;
			return true;
		}
	}

	public SwitchAction Resolve(DeviceId device, SwitchAction action) => action switch
	{
		SwitchAction.Toggle => IsOn(device) ? SwitchAction.Off : SwitchAction.On,
		_ => action
	};

	public IReadOnlyList<DeviceView> All(Func<DeviceId, DateTimeOffset?> holdUntil)
	{
		lock (_gate)
			return [.. Enum.GetValues<DeviceId>().Select(device => new DeviceView(device, _states[device], holdUntil(device)))];
	}
}

/// The physical outputs
public interface ILocalDeviceController
{
	bool Controls(DeviceId device);

	/// Devices driven by the same physical output
	IReadOnlyList<DeviceId> Linked(DeviceId device);

	Result<string> Apply(DeviceId device, PowerState state);
}

/// The desktop computer
public interface IComputerEndpoint
{
	bool Connected { get; }

	Result<string> WakeOnLan();

	Task<Result<string>> Send(ComputerCommand command, string argument, CancellationToken token = default);

	/// Waits until the pc has powered off
	Task WaitUntilPoweredOff(TimeSpan grace, CancellationToken token = default);
}
