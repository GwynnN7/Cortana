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
		var ipEndPoint = new IPEndPoint(IPAddress.Any, 5000);
		Server = new TcpListener(ipEndPoint);
	}
	
	public static async void StartListening()
	{
		Server.Start();
		
		var bListening = true;
		while (bListening)
		{
			try
			{
				TcpClient handler = await Server.AcceptTcpClientAsync();
				_ = Task.Run(async () => await HandleConnection(handler));
			}
			catch
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