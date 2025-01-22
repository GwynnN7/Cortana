using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: Newtonsoft.Json.JsonConstructor]
internal readonly struct SensorData(
    EPower bigMotion,
    EPower smallMotion,
    int light,
    int temperature,
    int humidity
    )
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower BigMotion { get; } = bigMotion;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower SmallMotion { get; } = smallMotion;
    public int Light { get; } = light;
    public int Temperature { get; } = temperature;
    public int Humidity { get; } = humidity;
}