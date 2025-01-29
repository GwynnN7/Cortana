using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Devices;
using Kernel.Hardware.SocketHandler;
using Kernel.Hardware.Utility;
using Kernel.Software;
using Kernel.Software.DataStructures;
using Kernel.Software.Extensions;

namespace Kernel.Hardware;

public static class HardwareApi
{
	public static NetworkData NetworkData => Service.NetworkData;
	
	private static readonly Lock RaspberryLock = new();
	private static readonly Lock ComputerLock = new();
	private static readonly Lock DeviceLock = new();

	public static void ShutdownServices()
	{
		ComputerHandler.Interrupt();
		SensorsHandler.Interrupt();
		ServerHandler.ShutdownServer();
	}
	public static void SubscribeNotification(Action<string> action, ENotificationPriority priority) => HardwareNotifier.Subscribe(action, priority);
	public static bool Ping(string address) => Helper.Ping(address);


	public static class Sensors
	{
		public static EControlMode ControlMode => Service.CurrentControlMode;
		public static Settings Settings => Service.Settings;
		
		public static string GetData(ESensor sensor)
		{
			switch (sensor)
			{
				case ESensor.Temperature:
					double? temp = SensorsHandler.GetRoomTemperature();
					if (temp is not null) return $"{Helper.FormatTemperature(temp.Value)}";
					break;
				case ESensor.Light:
					int? light = SensorsHandler.GetRoomLightLevel();
					if (light is not null) return $"{light}";
					break;
				case ESensor.Motion:
					EPower? motion = SensorsHandler.GetMotionDetected();
					if (motion is not null) return motion.Value == EPower.On ? "Motion Detected!" : "Motion not detected.";
					break;
			}
			return "Sensor offline";
		}
		
		public static string GetData(string sensor)
		{
			ESensor? sensorValue = sensor.ToEnum<ESensor>();
			return sensorValue.HasValue ? GetData(sensorValue.Value) : "Sensor offline";
		}
	}


	public static class Raspberry
	{
		public static string Command(ERaspberryOption option)
		{
			lock (RaspberryLock)
			{
				switch(option)
				{
					case ERaspberryOption.Shutdown:
						RaspberryHandler.Shutdown();
						break;
					case ERaspberryOption.Reboot:
						RaspberryHandler.Reboot();
						break;
					case ERaspberryOption.Update:
						RaspberryHandler.Update();
						break;
				}
				return "Command executed";
			}
		} 
		public static string GetHardwareInfo(EHardwareInfo hardwareInfo)
		{
			lock (RaspberryLock)
			{
				return hardwareInfo switch
				{
					EHardwareInfo.Location => RaspberryHandler.GetNetworkLocation().ToString(),
					EHardwareInfo.Ip => RaspberryHandler.RequestPublicIpv4().Result,
					EHardwareInfo.Gateway => RaspberryHandler.GetNetworkGateway(),
					EHardwareInfo.Temperature => Helper.FormatTemperature(RaspberryHandler.ReadCpuTemperature()),
					_ => throw new CortanaException("Hardware info not supported")
				};
			}
		}
		public static double ReadCpuTemperature()
		{
			lock (RaspberryLock)
			{
				return RaspberryHandler.ReadCpuTemperature();
			}
		}
	}

	
	public static class Devices
	{
		public static void EnterSleepMode() => Service.EnterSleepMode(true);
		public static string CommandComputer(EComputerCommand command, string? args = null)
		{
			lock (ComputerLock)
			{
				if (GetPower(EDevice.Computer) == EPower.Off) return "Computer is off";
				bool result = command switch
				{
					EComputerCommand.Shutdown => ComputerHandler.Shutdown(),
					EComputerCommand.Suspend => ComputerHandler.Suspend(),
					EComputerCommand.Notify => ComputerHandler.Notify(args ?? $"Still alive at {Raspberry.GetHardwareInfo(EHardwareInfo.Temperature)}"),
					EComputerCommand.Reboot => ComputerHandler.Reboot(),
					EComputerCommand.Command => ComputerHandler.Command(args ?? "dir"),
					EComputerCommand.SwapOs => ComputerHandler.SwapOs(),
					_ => false
				};
			
				string output = result ? "Command executed" : "Command not found";
				if(command == EComputerCommand.Command) output = (ComputerHandler.GatherMessage(out string? message) ? message : output)!;

				return output;
			}
		}
		
		public static EPower GetPower(EDevice device)
		{
			lock (DeviceLock)
			{
				return DeviceHandler.HardwareStates[device];
			}
		}
		public static string GetPower(string device)
		{
			EDevice? dev = device.ToEnum<EDevice>();
			return dev == null ? "Status not detectable" : $"{device.Capitalize()} is {GetPower(dev.Value)}";
		}
		
		public static string Switch(EDevice device, EPowerAction trigger)
		{
			lock (DeviceLock)
			{
				EPower result = device switch
				{
					EDevice.Computer => HandleComputer(trigger), //Check if power supply is off before turning on
					EDevice.Power => HandleComputerSupply(trigger), //Check if computer is off before removing power
					EDevice.Lamp => DeviceHandler.PowerLamp(trigger),
					EDevice.Generic => DeviceHandler.PowerGeneric(trigger),
					_ => throw new CortanaException("Device not supported")
				};
				return $"{device} switched {result}";
			}
		}
		public static string Switch(string device, string trigger)
		{
			EDevice? elementResult = device.ToEnum<EDevice>();
			EPowerAction? triggerResult = trigger.ToEnum<EPowerAction>();
			if (triggerResult == null) return "Invalid action";
			if (elementResult != null) return Switch(elementResult.Value, triggerResult.Value);
			return device == "room" ? SwitchRoom(triggerResult.Value) : "Hardware device not listed";
		}
		public static string SwitchRoom(EPowerAction action)
		{
			lock (DeviceLock)
			{
				Switch(EDevice.Lamp, action);
				Switch(EDevice.Power, action);
				
				return $"Devices switched {action}";
			}
		}
		
		private static EPower HandleComputer(EPowerAction action)
		{
			switch (action)
			{
				case EPowerAction.On:
					if (GetPower(EDevice.Power) == EPower.Off) DeviceHandler.PowerComputerSupply(EPowerAction.On);
					return DeviceHandler.PowerComputer(EPowerAction.On);
				case EPowerAction.Off:
					return DeviceHandler.PowerComputer(EPowerAction.Off);
				case EPowerAction.Toggle:
				default:
					return HandleComputer(Helper.ConvertToggle(EDevice.Computer));
			}
		}

		private static EPower HandleComputerSupply(EPowerAction action)
		{
			switch (action)
			{
				case EPowerAction.On:
					return HandleComputer(EPowerAction.On);
				case EPowerAction.Off when GetPower(EDevice.Computer) == EPower.On:
					Task.Run(async () =>
					{
						DeviceHandler.PowerComputer(EPowerAction.Off);
						await ComputerHandler.CheckForConnection();
						DeviceHandler.PowerComputerSupply(EPowerAction.Off);
					});
					return EPower.Off;
				case EPowerAction.Off:
					return DeviceHandler.PowerComputerSupply(EPowerAction.Off);
				case EPowerAction.Toggle:
				default:
					return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
			}
		}
	}
}