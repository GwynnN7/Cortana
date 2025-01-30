using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Devices;
using Kernel.Hardware.SocketHandler;
using Kernel.Software;
using Kernel.Software.DataStructures;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.Utility;

internal static class Service
{
	internal static readonly NetworkData NetworkData;
	internal static readonly Settings Settings;
	
	private static Timer? _controllerTimer;
	private static bool _morningMessage;

	internal static EControlMode CurrentControlMode { 
		get => (EControlMode) Math.Min((int) field, (int) Settings.LimitControlMode);
		private set;
	} = EControlMode.Automatic;
    
    static Service()
    {
        var orvietoNet = FileHandler.DeserializeJson<NetworkData>(Path.Combine(Helper.StoragePath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.DeserializeJson<NetworkData>(Path.Combine(Helper.StoragePath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
        Settings = Settings.Load();
        
        Task.Run(ServerHandler.StartListening);
        ResetControllerTimer();
    }
    
    internal static void ResetControllerTimer()
    {
	    _controllerTimer?.Destroy();
	    _controllerTimer = new Timer("controller-timer", null, ControllerCallback, ETimerType.Utility);
	    DateTime time = DateTime.Now.Minute >= 15 && DateTime.Now.Minute < 45 ? DateTime.Today.AddHours(DateTime.Now.Hour + 1) : DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(30);
	    _controllerTimer.Set((0, 30, 0));
    }

    private static Task ControllerCallback(object? sender)
    {
	    ResetControllerTimer();
	    
	    if (DateTime.Now.Hour <= 6)
	    {
		    _morningMessage = true;
		    
		    if (HardwareApi.Devices.GetPower(EDevice.Computer) == EPower.Off)
		    {
			    EnterSleepMode();
			    return Task.CompletedTask;
		    }
		    
		    if (DateTime.Now.Hour % 2 != 0)
		    {
			    HardwareNotifier.Publish("You should go to sleep", ENotificationPriority.High);
		    }
	    }
	    else
	    {
		    if(_morningMessage) HardwareNotifier.Publish("Good morning, switching to Automatic Mode", ENotificationPriority.High);
		    CurrentControlMode = EControlMode.Automatic;
		    _morningMessage = false;
	    }
    	
    	return Task.CompletedTask;
    }
    
    internal static void EnterSleepMode(bool userAction = false)
    {
	    CurrentControlMode = EControlMode.Night;
	    
	    switch (userAction)
	    {
		    case true:
			    ResetControllerTimer();
			    break;
		    case false when HardwareApi.Devices.GetPower(EDevice.Lamp) == EPower.Off:
			    return;
	    }

	    HardwareNotifier.Publish("Good night, switching to Night Mode", ENotificationPriority.Low);
	    
	    if (userAction || (!userAction && CurrentControlMode != EControlMode.Manual))
	    {
		    HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Off);
	    }
    }
}