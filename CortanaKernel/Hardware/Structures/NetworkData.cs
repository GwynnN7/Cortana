using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public readonly struct NetworkData {
	public ELocation Location { get; init; }
	public string DesktopIp { get; init; } 
	public string DesktopMac { get; init; }
	public string Gateway { get; init; }
	public int ApiPort { get; init; }
	public int ServerPort { get; init; }
}