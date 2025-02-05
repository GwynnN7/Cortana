using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib;
using CortanaLib.Structures;
using Timer = System.Timers.Timer;

namespace CortanaDesktop;

public static class CortanaDesktop
{
    internal static DesktopInfo DesktopInfo { get; private set; }
    private static Socket? _computerSocket;

    private static void Main()
    {
        DesktopInfo = GetClientInfo();
        string gateway = GetCortanaGateway().Result;
        string address = gateway[..^1] + DesktopInfo.CortanaIp;
        
        StartAliveTimer();

        while(true)
        {
            CreateSocketConnection(address, DesktopInfo.DesktopPort);
            
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
            string cortanaApi = DataHandler.Env("CORTANA_API");
            ResponseMessage result = await httpClient.GetFromJsonAsync<ResponseMessage>($"{cortanaApi}/{ERoute.Raspberry}/{ERaspberryInfo.Gateway}") ?? throw new CortanaException("Cortana offline");
            return result.Response;
        }
        catch{
            throw new CortanaException("Cortana not reachable, can't find correct address");
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
                    case "shutdown" or "suspend" or "reboot" or "system":
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

    private static DesktopInfo GetClientInfo()
	{
        string cortanaPath = DataHandler.Env("CORTANA_PATH");
		string confPath = Path.Combine(cortanaPath, DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDesktop)}/Settings.json"));
        if (!File.Exists(confPath)) throw new Exception("Unknown client connection info");
        return DataHandler.DeserializeJson<DesktopInfo>(confPath);
    }
}