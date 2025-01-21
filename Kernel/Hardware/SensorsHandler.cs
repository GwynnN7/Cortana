using System.Net.Sockets;
using System.Text;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware;

internal static class SensorsHandler
{
    private static Socket? _espSocket;
    private static Timer? _connectionTimer;
    //private static readonly Stack<string> Messages;
    
    internal static void BindSocket(Socket socket)
    {
        _espSocket?.Close();
		
        _espSocket = socket;
        _espSocket.SendTimeout = 2000;
        Task.Run(Read);
		
        //UpdateComputerStatus(EPower.On);
        RestartConnectionTimer();
    }
    
    private static void Read()
    {
        try
        {
            while (true)
            {
                var buffer = new byte[1024];
                int received = _espSocket!.Receive(buffer);
                string message = Encoding.UTF8.GetString(buffer, 0, received);
                if (received == 0) continue;

                if (int.TryParse(message, out int val))
                {
                    HardwareProxy.SwitchDevice(EDevice.Lamp, val == 1 ? EPowerAction.On : EPowerAction.Off);
                }
                /*
                switch (message)
                {
                    case "SYN":
                        //UpdateComputerStatus(EPower.On);
                        RestartConnectionTimer();
                        break;
                    default:
                        //Monitor.Enter(Messages);
                        //Messages.Push(message);
                        //Monitor.Pulse(Messages);
                        //Monitor.Exit(Messages);
                        break;
                }
                */
            }
        }
        catch (Exception ex)
        {
            Software.FileHandler.Log("SensorsHandler", $"Read Interrupted with error: {ex.Message}");
            DisconnectSocket();
        }
    }
    
    internal static void DisconnectSocket()
    {
        //Messages.Clear();
        _espSocket?.Close();
        _espSocket = null;
        //UpdateComputerStatus(EPower.Off);
    }
	
    private static void ResetConnection(object? sender, EventArgs e) 
    {
        DisconnectSocket();
        _connectionTimer?.Close();
    }

    private static void RestartConnectionTimer()
    {
        _connectionTimer?.Stop();
        _connectionTimer?.Close();
        _connectionTimer = new Timer("connection-timer", null, (10, 0, 0), ResetConnection, ETimerType.Utility);
    }
}