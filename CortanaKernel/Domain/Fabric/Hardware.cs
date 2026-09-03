using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Fabric;

/// Whatever can write to a source's outputs. One per kind of hardware, picked by source
public interface IChannelWriter
{
	bool Handles(string source);

	bool Controls(string channel);

	/// Channels driven by the same physical output
	IReadOnlyList<string> Linked(string channel);

	Result<string> Apply(string channel, PowerState state, bool pulse);
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
