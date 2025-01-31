using System.Net.Sockets;
using System.Text;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;
using Timer = CortanaLib.Timer;

namespace CortanaKernel.Hardware.SocketHandler;

public abstract class ClientHandler
{
	private readonly Lock _socketLock = new();
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
		HardwareNotifier.Publish($"{_deviceName} connected ~ {DateTime.Now}");
		
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
    	catch
    	{
		    DisconnectIfAvailable();
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
    	catch
    	{
		    DisconnectIfAvailable();
    		return false;
    	}
    }

    protected void DisconnectIfAvailable()
    {
	    lock (_socketLock)
	    {
		    if (_socket != null) DisconnectSocket();
	    }
    }
    
    protected virtual void DisconnectSocket()
    {
	    HardwareNotifier.Publish($"{_deviceName} disconnected at {DateTime.Now}");
	    _socket?.Close();
	    _socket = null;
    }
	
    private Task ResetConnection(object? sender) 
    {
	    DisconnectIfAvailable();
	    _connectionTimer?.Destroy();

	    return Task.CompletedTask;
    }

    private void RestartConnectionTimer()
    {
	    _connectionTimer?.Destroy();
	    _connectionTimer = new Timer("connection-timer", null, ResetConnection, ETimerType.Utility);
	    _connectionTimer.Set((DisconnectTime, 0, 0));
    }
}