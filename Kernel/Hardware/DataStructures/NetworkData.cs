using System.Text.Json.Serialization;

namespace Kernel.Hardware.DataStructures;

[method: JsonConstructor]
public readonly struct NetworkData(
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