using System.Text.Json.Serialization;

namespace CortanaClient;

[method: JsonConstructor]
internal readonly struct ClientInfo(
	string cortanaApi,
	string cortanaIp,
	int clientPort)
{
	public string CortanaApi { get; } = cortanaApi;
	public string CortanaIp { get; } = cortanaIp;
	public int ClientPort { get; } = clientPort;
}