using Mono.Unix;
using Mono.Unix.Native;

namespace CortanaLib;

public static class SignalHandler
{
    private static readonly UnixSignal[] SignalsList =
    [
        new(Signum.SIGTERM),
        new(Signum.SIGINT)
    ];

    public static Task WaitForInterrupt()
    {
        UnixSignal.WaitAny(SignalsList, Timeout.Infinite);
        return Task.CompletedTask;
    }
}