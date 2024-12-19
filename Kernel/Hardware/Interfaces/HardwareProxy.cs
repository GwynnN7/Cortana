using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.Interfaces;

public abstract class HardwareProxy: IHardwareAdapter
{
	static HardwareProxy()
	{
		ServerHandler.StartListening();
		
		_ = new Timer("night-handler", null, new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0), 
			HandleNightCallback, ETimerType.Utility, ETimerLoop.Daily);
	}
	private static void HandleNightCallback(object? sender, EventArgs e)
	{
		if (HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.Off) 
			SwitchDevice(EDevice.Lamp, EPowerAction.Off);
		else 
			CommandComputer(EComputerCommand.Notify, "You should go to sleep");

		if (DateTime.Now.Hour < 6) _ = new Timer("safety-night-handler", null, (0, 0, 1), HandleNightCallback, ETimerType.Utility);
	}
	
	public static double ReadCpuTemperature() => HardwareAdapter.ReadCpuTemperature();
	public static bool Ping(string address) => HardwareAdapter.Ping(address);

	public static string GetHardwareInfo(EHardwareInfo hardwareInfo) => HardwareAdapter.GetHardwareInfo(hardwareInfo);
	public static string SwitchRaspberry(EPowerOption option) => HardwareAdapter.SwitchRaspberry(option);
	public static EPower GetDevicePower(EDevice device) => HardwareAdapter.GetDevicePower(device);
	public static string GetDevicePower(string device)
	{
		EDevice? dev = Helper.HardwareDeviceFromString(device);
		return dev == null ? "Unknown device" : $"{device} is {HardwareAdapter.GetDevicePower(dev.Value)}";
	}

	public static string CommandComputer(EComputerCommand command, string? args = null)
	{
		string result = command switch
		{
			EComputerCommand.Notify => HardwareAdapter.CommandComputer(EComputerCommand.Notify, args ?? $"Still alive at {GetHardwareInfo(EHardwareInfo.Temperature)}"),
			EComputerCommand.Reboot when HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.On => HardwareAdapter.CommandComputer(EComputerCommand.Reboot),
			EComputerCommand.Reboot when HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.Off => SwitchDevice(EDevice.Computer, EPowerAction.On),
			_ => "Command not found"
		};
		return result;
	}
	
	public static string SwitchDevice(EDevice device, EPowerAction trigger)
	{
		return device switch
		{
			//Check if power supply is off before turning on
			EDevice.Computer => HandleComputer(trigger),
			//Check if computer is off before removing power
			EDevice.Power => HandleComputerSupply(trigger),
			//Nothing to check for the other devices
			_ => HardwareAdapter.SwitchDevice(device, trigger)
		};
	}

	public static string SwitchDevice(string device, string trigger)
	{
		EDevice? elementResult = Helper.HardwareDeviceFromString(device);
		EPowerAction? triggerResult = Helper.PowerActionFromString(trigger);
		if (triggerResult == null) return "Invalid action";
		if (elementResult != null) return SwitchDevice(elementResult.Value, triggerResult.Value);
		return device == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
	}
	
	public static string SwitchRoom(EPowerAction action)
	{
		SwitchDevice(EDevice.Lamp, action);
		SwitchDevice(EDevice.Power, action);

		return $"Devices switched {action}";
	}

	private static string HandleComputer(EPowerAction action)
	{
		switch (action)
		{
			case EPowerAction.On:
				if (HardwareAdapter.GetDevicePower(EDevice.Power) == EPower.Off)
					HardwareAdapter.SwitchDevice(EDevice.Power, EPowerAction.On);
				return HardwareAdapter.SwitchDevice(EDevice.Computer, EPowerAction.On);
			case EPowerAction.Off:
				return HardwareAdapter.SwitchDevice(EDevice.Computer, EPowerAction.Off);
			case EPowerAction.Toggle:
			default:
				return HandleComputer(Helper.ConvertToggle(EDevice.Computer));
		}
	}

	private static string HandleComputerSupply(EPowerAction action)
	{
		switch (action)
		{
			case EPowerAction.On:
				return HandleComputer(EPowerAction.On);
			case EPowerAction.Off when HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.On:
				Task.Run(async () =>
				{
					HardwareAdapter.SwitchDevice(EDevice.Computer, EPowerAction.Off);
					await ComputerService.CheckForConnection();
					HardwareAdapter.SwitchDevice(EDevice.Power, EPowerAction.Off);
				});
				return "Waiting for Computer to shut down";
			case EPowerAction.Off:
				return HardwareAdapter.SwitchDevice(EDevice.Power, EPowerAction.Off);
			case EPowerAction.Toggle:
			default:
				return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
		}
	}
}