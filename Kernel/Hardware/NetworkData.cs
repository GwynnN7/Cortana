using System.Text.Json.Serialization;
using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.Utility;

namespace Kernel.Hardware;

internal static class NetworkAdapter
{
	private static readonly NetworkData NetworkData;

	static NetworkAdapter()
	{
		string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
		if (!Path.Exists(Path.Combine(networkPath, "NetworkDataOrvieto.json"))) throw new CortanaException();
		var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
		var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
		NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
	}

	internal static ELocation Location => NetworkData.Location;
	internal static string ComputerMac => NetworkData.DesktopLanMac;
	internal static string ComputerIp => NetworkData.DesktopIp;
	internal static string DesktopRoot => NetworkData.DesktopRoot;
	internal static string DesktopUsername => NetworkData.DesktopUsername;
}


[method: JsonConstructor]
internal readonly struct NetworkData(
	ELocation location,
	string cortanaIp,
	string cortanaLanMac,
	string cortanaWlanMac,
	string desktopIp,
	string desktopLanMac,
	string desktopWlanMac,
	string subnetMask,
	string gateway,
	string cortanaUsername,
	string desktopUsername,
	string desktopRoot)
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	internal ELocation Location { get; } = location;

	internal string CortanaIp { get; } = cortanaIp;
	internal string CortanaLanMac { get; } = cortanaLanMac;
	internal string CortanaWlanMac { get; } = cortanaWlanMac;
	internal string DesktopIp { get; } = desktopIp;
	internal string DesktopLanMac { get; } = desktopLanMac;
	internal string DesktopWlanMac { get; } = desktopWlanMac;
	internal string SubnetMask { get; } = subnetMask;
	internal string Gateway { get; } = gateway;
	internal string CortanaUsername { get; } = cortanaUsername;
	internal string DesktopUsername { get; } = desktopUsername;
	internal string DesktopRoot { get; } = desktopRoot;
}