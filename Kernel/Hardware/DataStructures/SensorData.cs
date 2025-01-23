using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: Newtonsoft.Json.JsonConstructor]
internal readonly struct SensorData(
    EPower bigMotion,
    EPower smallMotion,
    int light,
    float temperature
    )
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower BigMotion { get; } = bigMotion;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EPower SmallMotion { get; } = smallMotion;
    public int Light { get; } = light;
    public float Temperature { get; } = temperature;
}