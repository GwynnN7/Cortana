namespace CortanaClient;

internal readonly struct ClientInfo {
	public string CortanaApi { get; init; }
	public string CortanaIp { get; init; }
	public int ClientPort { get; init; }
	public string ClientPassword { get; init; }
}