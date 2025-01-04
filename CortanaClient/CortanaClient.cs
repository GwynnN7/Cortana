using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Timer = System.Timers.Timer;

namespace CortanaClient;

public static class ComputerClient
{
    internal static ClientInfo ClientInfo { get; private set; }
    private const string ClientInfoPath = "CortanaClient/Config/Client.json";
    private static Socket? _computerSocket;

    private static void Main()
    {
        ClientInfo = GetClientInfo();
        string gateway = GetCortanaGateway().Result;
        string address = gateway[..^1] + ClientInfo.CortanaIp;

        StartAliveTimer();

        while(true)
        {
            CreateSocketConnection(address, ClientInfo.ClientPort);
            
            Write("computer");
            Read();
            
            Thread.Sleep(1000);
        }
    }

    private static async Task<string> GetCortanaGateway()
    {
        using var httpClient = new HttpClient();
        try
        {
            string result = await httpClient.GetStringAsync($"http://{ClientInfo.CortanaApi}/api/raspberry/gateway");
            return result;
        }
        catch{
            throw new Exception("Cortana not reachable, can't find correct address");
        } 
    }

    private static void CreateSocketConnection(string address, int port)
    {
        try
        {
            _computerSocket?.Close();

            var ipEndPoint = new IPEndPoint(IPAddress.Parse(address), port);
            _computerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _computerSocket.SendTimeout = 2000;
            _computerSocket.Connect(ipEndPoint);
            
            OsHandler.ExecuteCommand("notify", "Cortana connected", false);
        }
        catch
        {
            DisconnectClient();
        }
    }
    
    private static void StartAliveTimer()
    {
        var timer = new Timer(4000);
        timer.Elapsed += (_, _) => Write("SYN");
        timer.Start();
    }
    
    private static void Read()
    {
        if(_computerSocket == null) return;

        string? textCommand = null;
        try
        {
            while (true)
            {
                var buffer = new byte[1024];
                int received = _computerSocket.Receive(buffer);
                string message = Encoding.UTF8.GetString(buffer, 0, received);
                if (received == 0) continue;

                switch (message)
                {
                    case "shutdown" or "suspend" or "reboot" or "swap-os":
                        OsHandler.ExecuteCommand(message);
                        break;
                    case "notify" or "cmd":
                        textCommand = message;
                        break;
                    default:
                        if(textCommand != null) OsHandler.ExecuteCommand(textCommand, message);
                        textCommand = null;
                        break;
                }
                Thread.Sleep(250);
            }
        }
        catch
        {
            DisconnectClient();
        }
    }

    internal static void Write(string message)
    {
        if(_computerSocket == null) return;

        try
        {
            _computerSocket.Send(Encoding.UTF8.GetBytes(message));
        }
        catch
        {
            DisconnectClient();
            OsHandler.ExecuteCommand("notify", "Cortana disconnected");
        }
    }

    private static void DisconnectClient()
    {
        _computerSocket?.Close();
        _computerSocket = null;
    }

    private static ClientInfo GetClientInfo()
	{
        string cortanaPath = Environment.GetEnvironmentVariable("CORTANA_PATH") ?? throw new Exception("Cortana path not set in env");
		string confPath = Path.Combine(cortanaPath, ClientInfoPath);
        if (!File.Exists(confPath)) throw new Exception("Unknown client connection info");

		try
		{
			string file = File.ReadAllText(confPath);
			return JsonConvert.DeserializeObject<ClientInfo>(file);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message, ex);
		}
	}
}