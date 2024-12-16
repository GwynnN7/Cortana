using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Kernel.Hardware;

public class ServerHandler
{
	public static async void Listener()
	{
		string hostName = Dns.GetHostName();
		IPHostEntry localhost = await Dns.GetHostEntryAsync(hostName);
		IPAddress localIpAddress = localhost.AddressList[0];
		
		var ipEndPoint = new IPEndPoint(localIpAddress, 5000); //IPAddress.Any
		TcpListener listener = new(ipEndPoint);

		try
		{    
			listener.Start();

			using TcpClient handler = await listener.AcceptTcpClientAsync();
			await using NetworkStream stream = handler.GetStream();

			const string message = "Message";
			byte[] dateTimeBytes = Encoding.UTF8.GetBytes(message);
			await stream.WriteAsync(dateTimeBytes);
		}
		finally
		{
			listener.Stop();
		}
	}
}