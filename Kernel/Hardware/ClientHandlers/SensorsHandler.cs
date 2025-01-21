using System.Net.Sockets;
using Kernel.Hardware.Interfaces;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.ClientHandlers;

internal class SensorsHandler(Socket socket) : ClientHandler(socket)
{
    private static SensorsHandler? _instance;
	private static readonly Lock InstanceLock = new();
	
	private Timer? _motionTimer;

	protected override void HandleRead(string message)
	{
		bool motionDetected = int.Parse(message) == 1;
		if (!motionDetected) return;
		
		_motionTimer?.Stop();
		_motionTimer?.Close();
			
		HardwareProxy.SwitchDevice(EDevice.Lamp, EPowerAction.On);
		HardwareNotifier.Publish("Motion detected, switching lamp on!");
			
		_motionTimer = new Timer("motion-timer", null, (10, 0, 0), MotionTimeout, ETimerType.Utility);
	}
	
	private Task MotionTimeout(object? sender) 
	{
		_motionTimer?.Close();
		if (HardwareProxy.GetDevicePower(EDevice.Computer) == EPower.On)
		{
			_motionTimer = new Timer("motion-timer", null, (10, 0, 0), MotionTimeout, ETimerType.Utility);
			return Task.CompletedTask;
		}
		HardwareProxy.SwitchDevice(EDevice.Lamp, EPowerAction.Off);
		HardwareNotifier.Publish("No motion detected, switching lamp off");

		return Task.CompletedTask;
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