using Mono.Unix;
using Mono.Unix.Native;

namespace CortanaLib.Runtime;

public static class ProcessSignals
{
	private static readonly UnixSignal[] Signals = [new(Signum.SIGTERM), new(Signum.SIGINT)];

	public static Task WaitForShutdown() => Task.Run(() => UnixSignal.WaitAny(Signals, Timeout.Infinite));
}
