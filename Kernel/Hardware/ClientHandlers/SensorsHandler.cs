using System.Net.Sockets;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware.ClientHandlers;

internal class SensorsHandler(Socket socket) : ClientHandler(socket)
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();

	protected override void HandleRead(string message)
	{
		HardwareNotifier.Publish(message);
	}
	
	internal static void BindNew(SensorsHandler sensorHandler)
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectSocket();
			_instance = sensorHandler;	
		}
	}
    
	internal static void Interrupt()
	{
		lock (InstanceLock)
		{
			_instance?.DisconnectSocket();
			_instance = null;
		}
	}
}