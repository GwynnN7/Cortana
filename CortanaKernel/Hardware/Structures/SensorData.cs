namespace CortanaKernel.Hardware.Structures;

public readonly struct SensorData
{
    public int Motion { get; init; }
    public int Light { get; init; }
    public double Temperature { get; init; }
    public double Humidity { get; init; }
    public int Eco2 { get; init; }
    public int Tvoc { get; init; }
}