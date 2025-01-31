using CortanaKernel.Hardware.Utility;
using Utility;
using Utility.Extensions;
using Utility.Structures;

namespace CortanaKernel.Hardware.Structures;

public class Settings
{
    private const int MaxAnalogRead = 4096;
    private static readonly string FilePath = Path.Combine(Helper.StoragePath, "Settings.json");

    public int LightThreshold
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, MaxAnalogRead);
            this.Serialize().Dump(FilePath);
        }
    } = 1500;

    public EControlMode LimitControlMode
    {
        get;
        set
        {
            field = value;
            this.Serialize().Dump(FilePath);
        }
    } = EControlMode.Automatic;
    
    public static Settings Load()
    {
        return FileHandler.DeserializeJson<Settings>(FilePath) ?? new Settings();
    }
}