using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Kernel.Hardware;

public static class ServerHandler
{
	private static readonly TcpListener Server;
	private static readonly List<TcpClient> Clients = [];
	
	static ServerHandler()
	{
		string hostName = Dns.GetHostName();
		IPHostEntry localhost = Dns.GetHostEntry(hostName);
		IPAddress localIpAddress = localhost.AddressList[0];
		
		var ipEndPoint = new IPEndPoint(IPAddress.Any, 5000); //IPAddress.Any
		Server = new TcpListener(ipEndPoint);
	}
	
	public static async void StartListening()
	{
		var bListening = true;
		while (bListening)
		{
			try
			{    
				Server.Start();

				TcpClient handler = await Server.AcceptTcpClientAsync();
				_ = Task.Run(async () => await HandleConnection(handler));
			}
			finally
			{
				Server.Stop();
				bListening = false;
			}
		}
	}

	private static async Task HandleConnection(TcpClient handler)
	{
		await using NetworkStream stream = handler.GetStream();
		
		var buffer = new byte[1_024];
		int received = await stream.ReadAsync(buffer);
		string message = Encoding.UTF8.GetString(buffer, 0, received);

		string answer;
		switch (message)
		{
			case "computer":
				answer = "ACK";
				ComputerService.BindClient(handler);
				Clients.Add(handler);
				break;
			case "esp32":
				answer = "Not yet implemented";
				break;
			default:
				answer = "Unknown Client";
				break;
		}
		
		await stream.WriteAsync(Encoding.UTF8.GetBytes(answer));
		if(answer != "ACK") handler.Close();
	}
}