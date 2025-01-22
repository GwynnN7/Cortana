using Kernel.Hardware.DataStructures;
using Kernel.Software;

namespace Kernel.Hardware;

public static class HardwareSettings
{
    public static readonly NetworkData NetworkData;
    internal static EControlMode HardwareControlMode = EControlMode.NightHandler;

    static HardwareSettings()
    {
        string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
        var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
    }
}