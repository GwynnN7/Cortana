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
	public ELocation Location { get; } = location;

	public string CortanaIp { get; } = cortanaIp;
	public string CortanaLanMac { get; } = cortanaLanMac;
	public string CortanaWlanMac { get; } = cortanaWlanMac;
	public string DesktopIp { get; } = desktopIp;
	public string DesktopLanMac { get; } = desktopLanMac;
	public string DesktopWlanMac { get; } = desktopWlanMac;
	public string SubnetMask { get; } = subnetMask;
	public string Gateway { get; } = gateway;
	public string CortanaUsername { get; } = cortanaUsername;
	public string DesktopUsername { get; } = desktopUsername;
	public string DesktopRoot { get; } = desktopRoot;
}