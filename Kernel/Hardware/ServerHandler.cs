using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Kernel.Hardware;

public static class ServerHandler
{
	private static readonly Socket Server;
	private static readonly List<Socket> Clients = [];
	
	static ServerHandler()
	{
		var ipEndPoint = new IPEndPoint(IPAddress.Any, 5000);
		Server = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
		Server.Bind(ipEndPoint);
	}
	
	public static async void StartListening()
	{
		Server.Listen();
		
		var bListening = true;
		while (bListening)
		{
			try
			{
				Socket socket = await Server.AcceptAsync();
				_ = Task.Run(() => HandleConnection(socket));
			}
			catch
			{
				Server.Close();
				Server.Dispose();
				bListening = false;
			}
		}
	}

	private static void HandleConnection(Socket socket)
	{
		var buffer = new byte[1_024];
		int received = socket.Receive(buffer);
		string message = Encoding.UTF8.GetString(buffer, 0, received);

		string answer;
		switch (message)
		{
			case "computer":
				answer = "ACK";
				ComputerService.BindClient(socket);
				Clients.Add(socket);
				break;
			case "esp32":
				answer = "Not yet implemented";
				break;
			default:
				answer = "Unknown Client";
				break;
		}
		
		socket.Send(Encoding.UTF8.GetBytes(answer));
		if (answer == "ACK") return;
		socket.Close();
		socket.Dispose();
	}
}