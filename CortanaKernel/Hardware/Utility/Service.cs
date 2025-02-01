using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Structures;
using CortanaLib;
using CortanaLib.Structures;
using Timer = CortanaLib.Timer;

namespace CortanaKernel.Hardware.Utility;

public static class Service
{
	public static readonly NetworkData NetworkData;
	public static readonly Settings Settings;
	
	private static Timer? _controllerTimer;
	private static bool _morningMessage;

	public static EControlMode CurrentControlMode { 
		get => (EControlMode) Math.Min((int) field, (int) Settings.LimitControlMode);
		private set;
	} = EControlMode.Automatic;
    
    static Service()
    {
        var orvietoNet = FileHandler.DeserializeJson<NetworkData>(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaKernel)}/NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.DeserializeJson<NetworkData>(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaKernel)}/NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
        Settings = Settings.Load();
        
        Task.Run(ServerHandler.StartListening);
        ResetControllerTimer();
    }
    
    public static void ResetControllerTimer()
    {
	    _controllerTimer?.Destroy();
	    _controllerTimer = new Timer("controller-timer", null, ControllerCallback, ETimerType.Utility);
	    DateTime time = DateTime.Now.Minute >= 15 && DateTime.Now.Minute < 45 ? 
		    DateTime.Today.AddHours(DateTime.Now.Hour + 1) : 
		    DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(30);
	    if (time < DateTime.Now) time = time.AddHours(1);
	    _controllerTimer.Set(time);
    }

    private static Task ControllerCallback(object? sender)
    {
	    ResetControllerTimer();
	    
	    if (DateTime.Now.Hour <= 6)
	    {
		    _morningMessage = true;
		    
		    if (HardwareApi.Devices.GetPower(EDevice.Computer) == EPowerStatus.Off)
		    {
			    EnterSleepMode();
			    return Task.CompletedTask;
		    }
		    
		    if (DateTime.Now.Hour % 2 != 0)
		    {
			    HardwareNotifier.Publish("You should go to sleep");
		    }
	    }
	    else
	    {
		    if(_morningMessage) HardwareNotifier.Publish("Good morning, switching to Automatic Mode");
		    CurrentControlMode = EControlMode.Automatic;
		    _morningMessage = false;
	    }
    	
    	return Task.CompletedTask;
    }
    
    public static void EnterSleepMode(bool userAction = false)
    {
	    CurrentControlMode = EControlMode.Night;
	    
	    switch (userAction)
	    {
		    case true:
			    ResetControllerTimer();
			    break;
		    case false when HardwareApi.Devices.GetPower(EDevice.Lamp) == EPowerStatus.Off:
			    return;
	    }

	    HardwareNotifier.Publish("Good night, switching to Night Mode");
	    
	    if (userAction || (!userAction && CurrentControlMode != EControlMode.Manual))
	    {
		    HardwareApi.Devices.Switch(EDevice.Lamp, EPowerAction.Off);
	    }
    }
}