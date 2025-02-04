using Mono.Unix;
using Mono.Unix.Native;

namespace CortanaLib;

public static class Signals
{
    private static readonly UnixSignal[] SignalsList =
    [
        new(Signum.SIGTERM), 
        new(Signum.SIGINT),
        new(Signum.SIGUSR1)
    ];

    public static Task WaitForInterrupt()
    {
        UnixSignal.WaitAny(SignalsList, Timeout.Infinite);
        return Task.CompletedTask;
    }
}