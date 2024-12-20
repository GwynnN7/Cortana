using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace CortanaClient;

internal enum Os
{
    Linux,
    Windows
}

public static class ComputerClient
{
    private const string BashPath = "/bin/bash";
    private const string CmdPath = "cmd.exe";

    private static Os _operatingSystem;
    private static Socket? _computerSocket;

    private static void Main()
    {
        GetOperatingSystem();

        while(true)
        {
            CreateSocketConnection();
            Write("computer");

            Read();
        }
    }

    private static void GetOperatingSystem()
    {
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))  _operatingSystem = Os.Linux;
        else if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) _operatingSystem = Os.Windows;
        else throw new Exception("Unsupported Operating System");
    }

    private static void CreateSocketConnection()
    {
        try
        {
            _computerSocket?.Close();

            var ipEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.117"), 5000);
            _computerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _computerSocket.Connect(ipEndPoint);
        }
        catch
        {
            _computerSocket?.Close();
        }
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
                    case "shutdown":
                        ExecuteCommand(_operatingSystem == Os.Linux ? "poweroff" : "shutdown /s");
                        break;
                    case "reboot":
                        ExecuteCommand(_operatingSystem == Os.Linux ? "reboot" : "shutdown /r");
                        break;
                    case "notify":
                        bNotifyText = true;
                        break;
                    default:
                        if(bNotifyText)
                        {
                            ExecuteCommand(_operatingSystem == Os.Linux ? $"notify-send -u low -a Cortana \'{message}\'" : $"echo {message}");
                            bNotifyText = false;
                        }
                        break;
                }
            }
        }
        catch
        {
            ExecuteCommand(_operatingSystem == Os.Linux ? $"notify-send -u low -a Cortana \'Computer Disconnected\'" : $"echo Computer Disconnected");
        }
    }

    private static void Write(string message)
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

    private static void ExecuteCommand(string arg)
    {
        string path = _operatingSystem == Os.Linux ? BashPath : CmdPath;
        string param = _operatingSystem == Os.Linux ? "-c" : "/C";
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = $"{param} \"{arg}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };
        process.Start();
    }
}