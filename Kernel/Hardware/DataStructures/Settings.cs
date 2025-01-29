using Kernel.Software;
using Kernel.Software.DataStructures;

namespace Kernel.Hardware.DataStructures;

public class Settings : ISerializable
{
    private const int MaxAnalogRead = 4096;
    public int LightThreshold
    {
        get;
        set => field = Math.Clamp(value, 0, MaxAnalogRead);
    } = 1500;
    public EControlMode LimitControlMode { get; set; } = EControlMode.Automatic;
    
    public void Serialize(string path)
    {
        FileHandler.SerializeObject(this, path);
    }

    public static Settings Load(string path)
    {
        return FileHandler.Deserialize<Settings>(path) ?? new Settings();
    }
}