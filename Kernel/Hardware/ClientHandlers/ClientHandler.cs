using System.Net.Sockets;
using System.Text;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.ClientHandlers;

internal abstract class ClientHandler
{
	private Socket? _socket;
	private Timer? _connectionTimer;
	
	private const int Timeout = 2000;
	private const int DisconnectTime = 10;

	protected ClientHandler(Socket socket)
	{
		_socket = socket;
		_socket.SendTimeout = Timeout;
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
	    _socket?.Close();
	    _socket = null;
    }
	
    private Task ResetConnection(object? sender) 
    {
	    DisconnectSocket();
	    _connectionTimer?.Close();

	    return Task.CompletedTask;
    }

    protected void RestartConnectionTimer()
    {
	    _connectionTimer?.Stop();
	    _connectionTimer?.Close();
	    _connectionTimer = new Timer("connection-timer", null, (DisconnectTime, 0, 0), ResetConnection, ETimerType.Utility);
    }
}