using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Devices;
using Kernel.Software;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.Utility;

internal static class Service
{
	internal static readonly NetworkData NetworkData;
	internal static Settings Settings;
	
	private static Timer? _controllerTimer;
	private static bool _morningMessage;
	private static EControlMode _controlMode = EControlMode.Automatic;
    internal static EControlMode CurrentControlMode => 
	    Math.Min( (int) _controlMode, (int) Settings.LimitControlMode) switch
	    {
		    0 => EControlMode.Manual, 1 => EControlMode.Night, 2 => EControlMode.Automatic,
		    _ => _controlMode
	    };
    
    static Service()
    {
        string networkPath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Network/");
        var orvietoNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataOrvieto.json"));
        var pisaNet = FileHandler.LoadFile<NetworkData>(Path.Combine(networkPath, "NetworkDataPisa.json"));
		
        NetworkData = RaspberryHandler.GetNetworkGateway() == orvietoNet.Gateway ? orvietoNet : pisaNet;
        Settings = new Settings(1500, EControlMode.Automatic);
    }
    
    internal static void ResetControllerTimer()
    {
	    _controllerTimer?.Destroy();
	    _controllerTimer = new Timer("controller-timer", null, ControllerCallback, ETimerType.Utility);
	    DateTime time = DateTime.Now.Minute >= 15 && DateTime.Now.Minute < 45 ? DateTime.Today.AddHours(DateTime.Now.Hour + 1) : DateTime.Today.AddHours(DateTime.Now.Hour).AddMinutes(30);
	    _controllerTimer.Set(time);
    }

    private static Task ControllerCallback(object? sender)
    {
	    if (DateTime.Now.Hour <= 6)
	    {
		    if (HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.Off)
		    {
			    EnterSleepMode();
			    return Task.CompletedTask;
		    }
		    
		    if (DateTime.Now.Hour % 2 != 0)
		    {
			    HardwareNotifier.Publish("You should go to sleep", ENotificationPriority.High);
		    }
		    _morningMessage = true;
	    }
	    else
	    {
		    if(_morningMessage) HardwareNotifier.Publish("Good morning, switching to Automatic Mode", ENotificationPriority.High);
		    _controlMode = EControlMode.Automatic;
		    _morningMessage = false;
	    }
    
	    ResetControllerTimer();
    	
    	return Task.CompletedTask;
    }
    
    internal static void EnterSleepMode(bool userAction = false)
    {
	    _controlMode = EControlMode.Night;
	    
	    if (!userAction && HardwareAdapter.GetDevicePower(EDevice.Lamp) == EPower.Off) return;
	    HardwareNotifier.Publish("Good night, switching to Night Mode", ENotificationPriority.Low);
	    
	    if (userAction || (!userAction && CurrentControlMode != EControlMode.Manual))
	    {
		    HardwareAdapter.SwitchDevice(EDevice.Lamp, EPowerAction.Off);
	    }
	    
	    ResetControllerTimer();
    }
}