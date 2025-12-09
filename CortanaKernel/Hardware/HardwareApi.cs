global using StringResult = CortanaLib.Structures.Result<string, string>;
using System.Globalization;
using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Utility;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware;

public static class HardwareApi
{
	private static readonly Lock RaspberryLock = new();
	private static readonly Lock ComputerLock = new();
	private static readonly Lock DeviceLock = new();

	public static void InitializeHardware()
	{
		DeviceHandler.LoadDevices();
		ServerHandler.Initialize();
		Service.Start();
	}

	public static void ShutdownService()
	{
		ComputerHandler.Interrupt();
		SensorsHandler.Interrupt();
		ServerHandler.ShutdownServer();
		Service.InterruptController();
	}

	public static class Sensors
	{
		public static StringResult GetData(ESensor sensor)
		{
			switch (sensor)
			{
				case ESensor.Temperature:
					double? temp = SensorsHandler.GetRoomTemperature();
					if (temp is not null) return StringResult.Success(Math.Round(temp.Value, 1).ToString(CultureInfo.CurrentCulture));
					break;
				case ESensor.Light:
					int? light = SensorsHandler.GetRoomLightLevel();
					if (light.HasValue) return StringResult.Success(light.Value.ToString());
					break;
				case ESensor.Motion:
					EPowerStatus? motion = SensorsHandler.GetMotionDetected();
					if (motion is not null) return StringResult.Success((motion.Value == EPowerStatus.On).ToString().ToLower());
					break;
			}
			return StringResult.Failure("Sensor offline");
		}

		public static StringResult GetSettings(ESettings settings)
		{
			return settings switch
			{
				ESettings.LightThreshold => StringResult.Success(Service.Settings.LightThreshold.ToString()),
				ESettings.MotionDetection => StringResult.Success(Service.Settings.MotionDetection.ToString()),
				ESettings.MorningHour => StringResult.Success(Service.Settings.MorningHour.ToString()),
				ESettings.MotionOffMax => StringResult.Success(Service.Settings.MotionOffMax.ToString()),
				ESettings.MotionOffMin => StringResult.Success(Service.Settings.MotionOffMin.ToString()),
				_ => StringResult.Failure("Settings not found")
			};
		}

		public static StringResult SetSettings(ESettings settings, int value)
		{
			switch (settings)
			{
				case ESettings.LightThreshold:
					Service.Settings.LightThreshold = value;
					break;
				case ESettings.MotionDetection:
					if (value != (int)EMotionDetection.On && value != (int)EMotionDetection.Off) value = (int)(Service.Settings.MotionDetection == EMotionDetection.On ? EMotionDetection.Off : EMotionDetection.On);
					Service.Settings.MotionDetection = (EMotionDetection)value;
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
				switch (option)
				{
					case ERaspberryCommand.Shutdown:
						RaspberryHandler.Shutdown();
						break;
					case ERaspberryCommand.Reboot:
						RaspberryHandler.Reboot();
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
					EComputerCommand.Notify => ComputerHandler.Notify(args ?? $"Still alive at {Raspberry.GetHardwareInfo(ERaspberryInfo.Temperature).Value}"),
					EComputerCommand.Reboot => ComputerHandler.Reboot(),
					EComputerCommand.Command => ComputerHandler.Command(args ?? "dir"),
					EComputerCommand.System => ComputerHandler.SwitchOs(),
					_ => false
				};

				if (!result) return StringResult.Failure("Command not found");

				string output = "Command executed";
				if (command == EComputerCommand.Command) output = (ComputerHandler.GatherMessage(out string? message) ? message : output)!;

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
				return !result.HasValue ? StringResult.Failure("Device not supported") : StringResult.Success(result.Value.ToString());
			}
		}

		public static StringResult SwitchRoom(EPowerAction action)
		{
			lock (DeviceLock)
			{
				if (action == EPowerAction.On)
				{
					if (SensorsHandler.GetRoomLightLevel().GetValueOrDefault(0) <= Service.Settings.LightThreshold)
					{
						Switch(EDevice.Lamp, action);
					}
				}
				else Switch(EDevice.Lamp, action);
				StringResult powerResult = Switch(EDevice.Power, action);

				return powerResult.IsOk ? StringResult.Success(action.ToString()) : StringResult.Failure("Devices failed to switch");
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