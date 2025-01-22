using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: JsonConstructor]
internal readonly struct SensorData(
    EPower bigMotion,
    EPower smallMotion,
    int light,
    int temp,
    int hum
    )
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower BigMotion { get; } = bigMotion;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower SmallMotion { get; } = smallMotion;
    public int Light { get; } = light;
    public int Temperature { get; } = temp;
    public int Humidity { get; } = hum;
}