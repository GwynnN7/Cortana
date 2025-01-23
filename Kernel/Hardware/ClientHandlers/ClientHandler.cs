using System.Net.Sockets;
using System.Text;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.ClientHandlers;

internal abstract class ClientHandler
{
	private Socket? _socket;
	private Timer? _connectionTimer;
	private readonly string _deviceName;
	
	private const int Timeout = 2000;
	private const int DisconnectTime = 10;

	protected ClientHandler(Socket socket, string deviceName)
	{
		_deviceName = deviceName;
		_socket = socket;
		_socket.SendTimeout = Timeout;
		HardwareNotifier.Publish($"{_deviceName} connected at {DateTime.Now}", ENotificationPriority.Low);
		
		Task.Run(Read);
		RestartConnectionTimer();
	}

    private void Read()
    {
    	try
    	{
    		while (true)
    		{
    			var buffer = new byte[1024];
    			int received = _socket!.Receive(buffer);
    			string message = Encoding.UTF8.GetString(buffer, 0, received);
    			if (received == 0) continue;

			    RestartConnectionTimer();
			    HandleRead(message);
		    }
    	}
    	catch (Exception ex)
    	{
    		Software.FileHandler.Log(GetType().Name, $"Read Interrupted with error: {ex.Message}");
    		DisconnectSocket();
    	}
    }
    protected abstract void HandleRead(string message);

    protected bool Write(string message)
    {
    	try
    	{
    		_socket!.Send(Encoding.UTF8.GetBytes(message));
    		return true;
    	}
    	catch (Exception ex)
    	{
    		Software.FileHandler.Log(GetType().Name, $"Write of message \"{message}\" failed with error: {ex.Message}");
    		DisconnectSocket();
    		return false;
    	}
    }
    
    protected virtual void DisconnectSocket()
    {
	    if (_socket != null)
	    {
		    HardwareNotifier.Publish($"{_deviceName} disconnected at {DateTime.Now}", ENotificationPriority.Low);
		    _socket?.Close();
	    }
	    _socket = null;
    }
	
    private Task ResetConnection(object? sender) 
    {
	    DisconnectSocket();
	    _connectionTimer?.Close();

	    return Task.CompletedTask;
    }

    private void RestartConnectionTimer()
    {
	    _connectionTimer?.Stop();
	    _connectionTimer?.Close();
	    _connectionTimer = new Timer("connection-timer", null, ResetConnection, ETimerType.Utility);
	    _connectionTimer.Set((DisconnectTime, 0, 0));
    }
}