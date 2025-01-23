using Kernel.Hardware.ClientHandlers;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;
using Timer = Kernel.Software.Timer;

namespace Kernel.Hardware.Interfaces;

public abstract class HardwareProxy: IHardwareAdapter
{
	private static readonly Lock RaspberryLock = new();
	private static readonly Lock ComputerLock = new();
	private static readonly Lock DeviceLock = new();

	private static Timer? _nightHandler;
	
	static HardwareProxy()
	{
		ResetNightHandler();
	}

	internal static void ResetNightHandler()
	{
		_nightHandler?.Destroy();
		_nightHandler = new Timer("night-handler", null, HandleNightCallback, ETimerType.Utility);

		if (DateTime.Now.Hour >= 7)
		{
			_nightHandler.Set(new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 1, 0, 0));
			HardwareSettings.CurrentControlMode = EControlMode.MotionSensor;
		}
		else
			_nightHandler.Set((0, 30, 0));
	}
	
	private static Task HandleNightCallback(object? sender)
	{
		ResetNightHandler();
		if (HardwareSettings.CurrentControlMode == EControlMode.Manual) return Task.CompletedTask;
		
		if (GetDevicePower(EDevice.Computer) == EPower.Off)
		{
			HardwareSettings.CurrentControlMode = EControlMode.NightHandler;
			if (GetDevicePower(EDevice.Lamp) == EPower.Off) return Task.CompletedTask;
			
			SwitchDevice(EDevice.Lamp, EPowerAction.Off);
			HardwareNotifier.Publish("Night Handler activated, lamp switched off", ENotificationPriority.Low);
		}
		else if(DateTime.Now.Hour % 2 != 0) HardwareNotifier.Publish("You should go to sleep", ENotificationPriority.High);
		
		return Task.CompletedTask;
	}
	
	public static void ShutdownServices() => HardwareAdapter.ShutdownServices();
	public static bool Ping(string address) => HardwareAdapter.Ping(address);

	public static double ReadCpuTemperature()
	{
		lock (RaspberryLock)
		{
			return HardwareAdapter.ReadCpuTemperature();
		}
	}

	public static string GetHardwareInfo(EHardwareInfo hardwareInfo)
	{
		lock (RaspberryLock)
		{
			return HardwareAdapter.GetHardwareInfo(hardwareInfo);
		}
	}

	public static string GetSensorInfo(ESensor sensor) => HardwareAdapter.GetSensorInfo(sensor);
	public static string GetSensorInfo(string sensor) => HardwareAdapter.GetSensorInfo(sensor);

	public static string CommandRaspberry(ERaspberryOption option)
	{
		lock (RaspberryLock)
		{
			return HardwareAdapter.CommandRaspberry(option);
		}
	} 

	public static EPower GetDevicePower(EDevice device)
	{
		lock (DeviceLock)
		{
			return HardwareAdapter.GetDevicePower(device);
		}
	}
	
	public static string GetDevicePower(string device)
	{
		EDevice? dev = Helper.EnumFromString<EDevice>(device);
		return dev == null ? "Status not detectable" : $"{Helper.CapitalizeLetter(device)} is {GetDevicePower(dev.Value)}";
	}

	public static string CommandComputer(EComputerCommand command, string? args = null)
	{
		lock (ComputerLock)
		{
			if (HardwareAdapter.GetDevicePower(EDevice.Computer) == EPower.Off) return "Computer is off";
			string result = command switch
			{
				EComputerCommand.Notify => HardwareAdapter.CommandComputer(EComputerCommand.Notify, args ?? $"Still alive at {GetHardwareInfo(EHardwareInfo.Temperature)}"),
				EComputerCommand.Command => GatherClientMessage(EComputerCommand.Command, args ?? "dir"),
				_ => HardwareAdapter.CommandComputer(command),
			};
			return result;
		}
	}

	private static string GatherClientMessage(EComputerCommand command, string args)
	{ 
		string result = HardwareAdapter.CommandComputer(command, args);
		return (ComputerHandler.GatherMessage(out string? message) ? message : result)!;
	}
	
	public static string SwitchDevice(EDevice device, EPowerAction trigger)
	{
		lock (DeviceLock)
		{
			return device switch
			{
				EDevice.Computer => HandleComputer(trigger), //Check if power supply is off before turning on
				EDevice.Power => HandleComputerSupply(trigger), //Check if computer is off before removing power
				_ => HardwareAdapter.SwitchDevice(device, trigger)
			};
		}
	}

	public static string SwitchDevice(string device, string trigger)
	{
		EDevice? elementResult = Helper.EnumFromString<EDevice>(device);
		EPowerAction? triggerResult = Helper.EnumFromString<EPowerAction>(trigger);
		if (triggerResult == null) return "Invalid action";
		if (elementResult != null) return SwitchDevice(elementResult.Value, triggerResult.Value);
		return device == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
	}
	
	public static string SwitchRoom(EPowerAction action)
	{
		lock (DeviceLock)
		{
			SwitchDevice(EDevice.Lamp, action);
			SwitchDevice(EDevice.Power, action);
			
			return $"Devices switched {action}";
		}
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
					await ComputerHandler.CheckForConnection();
					HardwareAdapter.SwitchDevice(EDevice.Power, EPowerAction.Off);
				});
				return "Waiting for Computer to shutdown";
			case EPowerAction.Off:
				return HardwareAdapter.SwitchDevice(EDevice.Power, EPowerAction.Off);
			case EPowerAction.Toggle:
			default:
				return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
		}
	}
}