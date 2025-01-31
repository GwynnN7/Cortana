using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public readonly struct SensorData
{
    public EPowerStatus WideMotion { get; init; }
    public EPowerStatus PreciseMotion { get; init; }
    public int Light { get; init; }
    public double Temperature { get; init; }
}