using Kernel.Hardware.DataStructures;
using Kernel.Hardware.SocketHandler;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware;

public abstract class HardwareAdapter: IHardwareAdapter
{
	public static EControlMode ControlMode => Service.CurrentControlMode;
	private static readonly Lock RaspberryLock = new();
	private static readonly Lock ComputerLock = new();
	private static readonly Lock DeviceLock = new();
	
	static HardwareAdapter()
	{
		Service.ResetControllerTimer();
	}

	// INTERFACE METHODS
	
	public static void SubscribeNotification(Action<string> action, ENotificationPriority priority) => Wrapper.SubscribeNotification(action, priority);
	public static NetworkData GetNetworkData() => Wrapper.GetNetworkData();
	public static Settings GetSettings() => Wrapper.GetSettings();
	public static bool Ping(string address) => Wrapper.Ping(address);
	public static string GetSensorInfo(ESensor sensor) => Wrapper.GetSensorInfo(sensor);
	public static void ShutdownServices() => Wrapper.ShutdownServices();
	
	public static double ReadCpuTemperature()
	{
		lock (RaspberryLock)
		{
			return Wrapper.ReadCpuTemperature();
		}
	}
	public static string GetHardwareInfo(EHardwareInfo hardwareInfo)
	{
		lock (RaspberryLock)
		{
			return Wrapper.GetHardwareInfo(hardwareInfo);
		}
	}
	public static string CommandRaspberry(ERaspberryOption option)
	{
		lock (RaspberryLock)
		{
			return Wrapper.CommandRaspberry(option);
		}
	} 
	public static string CommandComputer(EComputerCommand command, string? args = null)
	{
		lock (ComputerLock)
		{
			if (Wrapper.GetDevicePower(EDevice.Computer) == EPower.Off) return "Computer is off";
			string result = command switch
			{
				EComputerCommand.Notify => Wrapper.CommandComputer(EComputerCommand.Notify, args ?? $"Still alive at {GetHardwareInfo(EHardwareInfo.Temperature)}"),
				EComputerCommand.Command => GatherClientMessage(args ?? "dir"),
				_ => Wrapper.CommandComputer(command),
			};
			return result;
		}
	}
	public static string SwitchDevice(EDevice device, EPowerAction trigger)
	{
		lock (DeviceLock)
		{
			return device switch
			{
				EDevice.Computer => HandleComputer(trigger), //Check if power supply is off before turning on
				EDevice.Power => HandleComputerSupply(trigger), //Check if computer is off before removing power
				_ => Wrapper.SwitchDevice(device, trigger)
			};
		}
	}
	
	public static EPower GetDevicePower(EDevice device)
	{
		lock (DeviceLock)
		{
			return Wrapper.GetDevicePower(device);
		}
	}
	

	// CLASS METHODS
	
	public static void EnterSleepMode() => Service.EnterSleepMode(true);
	
	public static string SwitchRoom(EPowerAction action)
	{
		lock (DeviceLock)
		{
			SwitchDevice(EDevice.Lamp, action);
			SwitchDevice(EDevice.Power, action);
			
			return $"Devices switched {action}";
		}
	}
	
	public static string GetSensorInfo(string sensor)
	{
		ESensor? sensorValue = Helper.EnumFromString<ESensor>(sensor);
		return sensorValue.HasValue ? GetSensorInfo(sensorValue.Value) : "Sensor offline";
	}
	
	public static string SwitchDevice(string device, string trigger)
	{
		EDevice? elementResult = Helper.EnumFromString<EDevice>(device);
		EPowerAction? triggerResult = Helper.EnumFromString<EPowerAction>(trigger);
		if (triggerResult == null) return "Invalid action";
		if (elementResult != null) return SwitchDevice(elementResult.Value, triggerResult.Value);
		return device == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
	}
	
	public static string GetDevicePower(string device)
	{
		EDevice? dev = Helper.EnumFromString<EDevice>(device);
		return dev == null ? "Status not detectable" : $"{Helper.CapitalizeLetter(device)} is {GetDevicePower(dev.Value)}";
	}

	private static string GatherClientMessage(string args)
	{ 
		string result = Wrapper.CommandComputer(EComputerCommand.Command, args);
		return (ComputerHandler.GatherMessage(out string? message) ? message : result)!;
	}
	
	private static string HandleComputer(EPowerAction action)
	{
		switch (action)
		{
			case EPowerAction.On:
				if (Wrapper.GetDevicePower(EDevice.Power) == EPower.Off)
					Wrapper.SwitchDevice(EDevice.Power, EPowerAction.On);
				return Wrapper.SwitchDevice(EDevice.Computer, EPowerAction.On);
			case EPowerAction.Off:
				return Wrapper.SwitchDevice(EDevice.Computer, EPowerAction.Off);
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
			case EPowerAction.Off when Wrapper.GetDevicePower(EDevice.Computer) == EPower.On:
				Task.Run(async () =>
				{
					Wrapper.SwitchDevice(EDevice.Computer, EPowerAction.Off);
					await ComputerHandler.CheckForConnection();
					Wrapper.SwitchDevice(EDevice.Power, EPowerAction.Off);
				});
				return "Waiting for Computer to shutdown";
			case EPowerAction.Off:
				return Wrapper.SwitchDevice(EDevice.Power, EPowerAction.Off);
			case EPowerAction.Toggle:
			default:
				return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
		}
	}
}