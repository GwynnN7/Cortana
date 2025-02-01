global using StringResult = CortanaLib.Structures.Result<string, string>;
using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Structures;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware;

public static class HardwareApi
{
	private static readonly Lock RaspberryLock = new();
	private static readonly Lock ComputerLock = new();
	private static readonly Lock DeviceLock = new();

	public static void ShutdownService()
	{
		ComputerHandler.Interrupt();
		SensorsHandler.Interrupt();
		ServerHandler.ShutdownServer();
	}

	public static class Sensors
	{
		public static StringResult GetData(ESensor sensor)
		{
			switch (sensor)
			{
				case ESensor.Temperature:
					double? temp = SensorsHandler.GetRoomTemperature();
					if (temp is not null) return StringResult.Success($"{Helper.FormatTemperature(temp.Value)}");
					break;
				case ESensor.Light:
					int? light = SensorsHandler.GetRoomLightLevel();
					if (light is not null) return StringResult.Success($"{light}");
					break;
				case ESensor.Motion:
					EPowerStatus? motion = SensorsHandler.GetMotionDetected();
					if (motion is not null) return StringResult.Success(motion.Value == EPowerStatus.On ? "Motion detected" : "Motion not detected");
					break;
			}
			return StringResult.Failure("Sensor offline");
		}

		public static Result<int, string> GetSettings(ESettings settings)
		{
			return settings switch
			{
				ESettings.LightThreshold => Result<int, string>.Success(Service.Settings.LightThreshold),
				ESettings.LimitMode => Result<int, string>.Success((int)Service.Settings.LimitControlMode),
				ESettings.ControlMode => Result<int, string>.Success((int)Service.CurrentControlMode),
				ESettings.MorningHour => Result<int, string>.Success(Service.Settings.MorningHour),
				ESettings.MotionOffMax => Result<int, string>.Success(Service.Settings.MotionOffMax),
				ESettings.MotionOffMin => Result<int, string>.Success(Service.Settings.MotionOffMin),
				_ => Result<int, string>.Failure("Settings not found")
			};
		}
		
		public static Result<int, string> SetSettings(ESettings settings, int value)
		{
			switch (settings)
			{
				case ESettings.LightThreshold:
					Service.Settings.LightThreshold = value;
					break;
				case ESettings.LimitMode:
					Service.Settings.LimitControlMode = (EControlMode) Math.Clamp(value, (int) EControlMode.Manual, (int) EControlMode.Automatic);
					break;
				case ESettings.MorningHour:
					Service.Settings.MorningHour = value;
					break;
				case ESettings.MotionOffMax:
					Service.Settings.MotionOffMax = value;
					break;
				case ESettings.MotionOffMin:
					Service.Settings.MotionOffMin = value;
					break;
			}
			return GetSettings(settings);
		}
	}

	public static class Raspberry
	{
		public static StringResult Command(ERaspberryCommand option)
		{
			lock (RaspberryLock)
			{
				switch(option)
				{
					case ERaspberryCommand.Shutdown:
						RaspberryHandler.Shutdown();
						break;
					case ERaspberryCommand.Reboot:
						RaspberryHandler.Reboot();
						break;
					case ERaspberryCommand.Update:
						RaspberryHandler.Update();
						break;
					default:
						return StringResult.Failure("Command not found");
				}
				return StringResult.Success("Command executed");
			}
		} 
		public static StringResult GetHardwareInfo(ERaspberryInfo hardwareInfo)
		{
			lock (RaspberryLock)
			{
				string? result = hardwareInfo switch
				{
					ERaspberryInfo.Location => RaspberryHandler.GetNetworkLocation().ToString(),
					ERaspberryInfo.Ip => RaspberryHandler.RequestPublicIpv4().Result,
					ERaspberryInfo.Gateway => RaspberryHandler.GetNetworkGateway(),
					ERaspberryInfo.Temperature => Helper.FormatTemperature(RaspberryHandler.ReadCpuTemperature()),
					ERaspberryInfo.ApiPort => RaspberryHandler.GetApiPort().ToString(),
					_ => null
				};
				return result is null ? StringResult.Failure("Raspberry information not supported") : StringResult.Success(result);
			}
		}
	}

	
	public static class Devices
	{
		public static void EnterSleepMode() => Service.EnterSleepMode(true);
		public static StringResult CommandComputer(EComputerCommand command, string? args = null)
		{
			lock (ComputerLock)
			{
				if (GetPower(EDevice.Computer) == EPowerStatus.Off) return StringResult.Failure("Computer is off");
				bool result = command switch
				{
					EComputerCommand.Shutdown => ComputerHandler.Shutdown(),
					EComputerCommand.Suspend => ComputerHandler.Suspend(),
					EComputerCommand.Notify => ComputerHandler.Notify(args ?? $"Still alive at {Raspberry.GetHardwareInfo(ERaspberryInfo.Temperature)}"),
					EComputerCommand.Reboot => ComputerHandler.Reboot(),
					EComputerCommand.Command => ComputerHandler.Command(args ?? "dir"),
					EComputerCommand.System => ComputerHandler.SwitchOs(),
					_ => false
				};

				if (!result) return StringResult.Failure("Command not found");
				
				string output = "Command executed";
				if(command == EComputerCommand.Command) output = (ComputerHandler.GatherMessage(out string? message) ? message : output)!;

				return StringResult.Success(output);
			}
		}
		
		public static EPowerStatus GetPower(EDevice device)
		{
			lock (DeviceLock)
			{
				return DeviceHandler.DeviceStatus[device];
			}
		}
		
		public static StringResult Switch(EDevice device, EPowerAction trigger)
		{
			lock (DeviceLock)
			{
				EPowerStatus? result = device switch
				{
					EDevice.Computer => HandleComputer(trigger), //Check if power supply is off before turning on
					EDevice.Power => HandleComputerSupply(trigger), //Check if computer is off before removing power
					EDevice.Lamp => DeviceHandler.PowerLamp(trigger),
					EDevice.Generic => DeviceHandler.PowerGeneric(trigger),
					_ => null
				};
				return result is null ? StringResult.Failure("Device not supported") : StringResult.Success($"{device} switched {result}");
			}
		}
		
		public static StringResult SwitchRoom(EPowerAction action)
		{
			lock (DeviceLock)
			{
				StringResult lampResult = Switch(EDevice.Lamp, action);
				StringResult powerResult = Switch(EDevice.Power, action);

				if (lampResult.IsOk && powerResult.IsOk) return StringResult.Success($"Devices switched {action}");
				return StringResult.Failure("A device failed to switch");
			}
		}
		
		private static EPowerStatus HandleComputer(EPowerAction action)
		{
			switch (action)
			{
				case EPowerAction.On:
					if (GetPower(EDevice.Power) == EPowerStatus.Off) DeviceHandler.PowerComputerSupply(EPowerAction.On);
					return DeviceHandler.PowerComputer(EPowerAction.On);
				case EPowerAction.Off:
					return DeviceHandler.PowerComputer(EPowerAction.Off);
				case EPowerAction.Toggle:
				default:
					return HandleComputer(Helper.ConvertToggle(EDevice.Computer));
			}
		}

		private static EPowerStatus HandleComputerSupply(EPowerAction action)
		{
			switch (action)
			{
				case EPowerAction.On:
					return HandleComputer(EPowerAction.On);
				case EPowerAction.Off when GetPower(EDevice.Computer) == EPowerStatus.On:
					Task.Run(async () =>
					{
						DeviceHandler.PowerComputer(EPowerAction.Off);
						await ComputerHandler.CheckForConnection();
						DeviceHandler.PowerComputerSupply(EPowerAction.Off);
					});
					return EPowerStatus.Off;
				case EPowerAction.Off:
					return DeviceHandler.PowerComputerSupply(EPowerAction.Off);
				case EPowerAction.Toggle:
				default:
					return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
			}
		}
	}
}