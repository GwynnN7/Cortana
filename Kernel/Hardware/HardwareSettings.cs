using Kernel.Hardware.DataStructures;
using Kernel.Software;

namespace Kernel.Hardware;

public static class HardwareSettings
{
    public static readonly NetworkData NetworkData;
    
    private static EControlMode _currentControlMode = EControlMode.NightHandler;
    public static EControlMode CurrentControlMode
    {
        get => (EControlMode) Math.Min((int) _currentControlMode, (int) LimitControlMode);
        internal set => _currentControlMode = value;
    }
    public static EControlMode LimitControlMode { get; set; } = EControlMode.MotionSensor;
    
    static HardwareSettings()
    {
        string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
        var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
    }
}