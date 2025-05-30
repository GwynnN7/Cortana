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
            this.Serialize().Dump(FilePath);
        }
    } = 1500;

    public EMotionDetection MotionDetection
    {
        get;
        set
        {
            field = (EMotionDetection) Math.Clamp((int) value, (int) EMotionDetection.Off, (int) EMotionDetection.On);
            this.Serialize().Dump(FilePath);
        }
    } = EMotionDetection.On;

    public int MorningHour
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, 23);
            this.Serialize().Dump(FilePath);
        }
    } = 9;
    
    public int MotionOffMin
    {
        get;
        set
        {
            field = Math.Clamp(value, 0, MotionOffMax);
            this.Serialize().Dump(FilePath);
        }
    } = 1;
    
    public int MotionOffMax
    {
        get;
        set
        {
            field = Math.Clamp(value, MotionOffMin, 3600);
            this.Serialize().Dump(FilePath);
        }
    } = 30;
    
    public static Settings Load()
    {
        return FilePath.Load<Settings>();
    }
}