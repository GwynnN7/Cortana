using Kernel.Hardware.DataStructures;
using Kernel.Software;

namespace Kernel.Hardware;

public static class HardwareSettings
{
    public static readonly NetworkData NetworkData;
    public static int LightThreshold { get; set; } = 1000;
    
    private static EControlMode _currentControlMode = EControlMode.Night;
    public static EControlMode CurrentControlMode
    {
        get => Math.Min((int)_currentControlMode, (int)LimitControlMode) switch
        {
            0 => EControlMode.Manual, 1 => EControlMode.Night, 2 => EControlMode.Automatic,
            _ => _currentControlMode
        };
        internal set => _currentControlMode = value;
    }
    public static EControlMode LimitControlMode { get; set; } = EControlMode.Automatic;
    
    static HardwareSettings()
    {
        string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
        var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
    }
}