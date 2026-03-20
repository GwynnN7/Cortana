using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public class Settings
{
    private const int MaxAnalogRead = 4096;

    private static readonly string FilePath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Settings.json");

    public int LightThreshold
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, MaxAnalogRead);
        }
    } = 1500;

    public EMotionDetection AutomaticMode
    {
        get;
        set
        {
            field = (EMotionDetection)Math.Clamp((int)value, (int)EMotionDetection.Off, (int)EMotionDetection.On);
        }
    } = EMotionDetection.On;

    public int MorningHour
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 23);
        }
    } = 9;

    public int MotionOffMin
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, MotionOffMax);
        }
    } = 1;

    public int MotionOffMax
    {
        get;
        set
        {
            field = Math.Clamp(value, MotionOffMin, 3600);
        }
    } = 30;

    public void Save()
    {
        this.Serialize().Dump(FilePath);
    }

    public static Settings Load()
    {
        return FilePath.Load<Settings>();
    }
}