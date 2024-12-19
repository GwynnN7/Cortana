using System.Text.Json.Serialization;
using Kernel.Hardware.Utility;
using Kernel.Software;

namespace Kernel.Hardware;

public static class NetworkAdapter
{
	private static readonly NetworkData NetworkData;

	static NetworkAdapter()
	{
		string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
		var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
		var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
		NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
	}

	public static ELocation Location => NetworkData.Location;
	public static string ComputerMac => NetworkData.DesktopMac;
	public static string ComputerIp => NetworkData.DesktopIp;
	public static int ApiPort => NetworkData.ApiPort;
	public static int ServerPort => NetworkData.ServerPort;
}


[method: JsonConstructor]
internal readonly struct NetworkData(
	ELocation location,
	string desktopIp,
	string desktopMac,
	string gateway,
	int apiPort,
	int serverPort)
{
	[JsonConverter(typeof(JsonStringEnumConverter))]
	public ELocation Location { get; } = location;
	public string DesktopIp { get; } = desktopIp;
	public string DesktopMac { get; } = desktopMac;
	public string Gateway { get; } = gateway;
	public int ApiPort { get; } = apiPort;
	public int ServerPort { get; } = serverPort;
}