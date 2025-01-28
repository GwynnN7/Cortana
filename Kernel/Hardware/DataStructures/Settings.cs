using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: Newtonsoft.Json.JsonConstructor]
public struct Settings(
    int lightThreshold,
    EControlMode limitControlMode
    )
{
    public int LightThreshold { get; set; } = lightThreshold;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public EControlMode LimitControlMode { get; set;  } = limitControlMode;
}