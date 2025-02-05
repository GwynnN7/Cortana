namespace CortanaDesktop;

internal readonly struct DesktopInfo {
	public string NetworkAddr { get; init; }
	public int TcpPort { get; init; }
	public string DesktopPassword { get; init; }
}