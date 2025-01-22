using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: JsonConstructor]
internal readonly struct SensorData(
    EPower bigMotion,
    EPower smallMotion,
    int light,
    double temperature,
    double humidity
    )
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower BigMotion { get; } = bigMotion;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower SmallMotion { get; } = smallMotion;
    public int Light { get; } = light;
    public double Temperature { get; } = temperature;
    public double Humidity { get; } = humidity;
}