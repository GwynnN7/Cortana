namespace Kernel.Hardware.DataStructures;

internal readonly struct SensorData
{
    public EPower WideMotion { get; init; }
    public EPower PreciseMotion { get; init; }
    public int Light { get; init; }
    public double Temperature { get; init; }
}