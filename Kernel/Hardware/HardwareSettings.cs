using Kernel.Hardware.DataStructures;
using Kernel.Software;

namespace Kernel.Hardware;

public static class HardwareSettings
{
    public static readonly NetworkData NetworkData;
    public static EControlMode CurrentControlMode { get; internal set; } = EControlMode.NightHandler;
    public static EControlMode UserControlMode { get; set; } = EControlMode.MotionSensor;

    static HardwareSettings()
    {
        string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
        var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
    }
}