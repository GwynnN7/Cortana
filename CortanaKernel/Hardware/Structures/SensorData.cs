using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public readonly struct SensorData
{
    public int Motion { get; init; }
    public int Light { get; init; }
    public double Temperature { get; init; }
}