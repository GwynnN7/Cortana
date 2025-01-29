using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.DataStructures;

namespace Kernel.Hardware.DataStructures;

public class Settings : ISerializable
{
    private const int MaxAnalogRead = 4096;
    private static readonly string Path = System.IO.Path.Combine(Helper.StoragePath, "Settings.json");

    public int LightThreshold
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, MaxAnalogRead);
            Serialize(Path);
        }
    } = 1500;

    public EControlMode LimitControlMode
    {
        get;
        set
        {
            field = value;
            Serialize(Path);
        }
    } = EControlMode.Automatic;

    public void Serialize(string path)
    {
        FileHandler.SerializeObject(this, path);
    }

    public static Settings Load()
    {
        return FileHandler.Deserialize<Settings>(Path) ?? new Settings();
    }
}