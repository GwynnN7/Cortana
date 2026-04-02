using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public class Settings
{

    private static readonly string FilePath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Settings.json");

    public int LightThreshold { get; set; } = 50;
    public int Eco2Threshold { get; set; } = 1000;
    public int TvocThreshold { get; set; } = 600;

    public EStatus AutomaticMode
    {
        get;
        set
        {
            field = (EStatus)Math.Clamp((int)value, (int)EStatus.Off, (int)EStatus.On);
        }
    } = EStatus.On;

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