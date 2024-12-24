using Newtonsoft.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Timer = System.Timers.Timer;

namespace CortanaClient;

public static class ComputerClient
{
    private const string ClientInfoPath = "CortanaClient/Config/Client.json";
    private static Socket? _computerSocket;

    private static void Main()
    {
        ClientInfo info = GetClientInfo();
        string gateway = GetCortanaGateway(info).Result;
        string address = gateway[..^1] + info.CortanaIp;

        StartAliveTimer();

        while(true)
        {
            CreateSocketConnection(address, info.ClientPort);
            Write("computer");

            Read();
        }
    }

    private static async Task<string> GetCortanaGateway(ClientInfo info)
    {
        using var httpClient = new HttpClient();
        try
        {
            string result = await httpClient.GetStringAsync($"http://{info.CortanaApi}/api/raspberry/gateway");
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
            _computerSocket.Connect(ipEndPoint);
        }
        catch
        {
            _computerSocket?.Close();
        }
    }


    private static void StartAliveTimer()
    {
        var timer = new Timer();
        timer.Elapsed += (sender, e) => Write("SYN");;
        timer.Interval = 4000;
        timer.Start();
    }
    
    private static void Read()
    {
        if(_computerSocket == null) return;

        var bNotifyText = false;
        try
        {
            while (true)
            {
                var buffer = new byte[1024];
                int received = _computerSocket!.Receive(buffer);
                string message = Encoding.UTF8.GetString(buffer, 0, received);
                if (received == 0) continue;

                switch (message)
                {
                    case "SYN":
                        Write("ACK");
                        break;
                    case "shutdown" or "reboot":
                        string powerCommand = OsHandler.DecodeCommand(message);
                        OsHandler.ExecuteCommand(powerCommand);
                        break;
                    case "notify":
                        bNotifyText = true;
                        break;
                    default:
                        if(bNotifyText)
                        {
                            string notifyCommand = OsHandler.DecodeCommand("notify", message);
                            OsHandler.ExecuteCommand(notifyCommand);
                            bNotifyText = false;
                        }
                        break;
                }
            }
        }
        catch
        {
            string failCommand = OsHandler.DecodeCommand("notify", "Cortana Disconnected");
            OsHandler.ExecuteCommand(failCommand);
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
            _computerSocket.Close();
        }
    }

    private static ClientInfo GetClientInfo()
	{
        string cortanaPath = Environment.GetEnvironmentVariable("CORTANA_PATH") ?? throw new Exception("Cortana path not set in env");
		string confPath = Path.Combine(cortanaPath, ClientInfoPath);
        if (!File.Exists(confPath)) throw new Exception("Unknown Client Connection Info");

		try
		{
			string file = File.ReadAllText(Path.Combine(confPath, ClientInfoPath));
			return JsonConvert.DeserializeObject<ClientInfo>(file);
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message, ex);
		}
	}
}