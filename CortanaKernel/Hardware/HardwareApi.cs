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
				case ESensor.Humidity:
					double? humidity = SensorsHandler.GetRoomHumidity();
					if (humidity.HasValue) return StringResult.Success(Math.Round(humidity.Value, 1).ToString(CultureInfo.CurrentCulture));
					break;
				case ESensor.CO2:
					int? co2 = SensorsHandler.GetRoomEco2();
					if (co2.HasValue) return StringResult.Success(co2.Value.ToString());
					break;
				case ESensor.Tvoc:
					int? tvoc = SensorsHandler.GetRoomTvoc();
					if (tvoc.HasValue) return StringResult.Success(tvoc.Value.ToString());
					break;
				case ESensor.Motion:
					EStatus? motion = SensorsHandler.GetMotionDetected();
					if (motion is not null) return StringResult.Success((motion.Value == EStatus.On).ToString().ToLower());
					break;
			}
			return StringResult.Failure("Sensor offline");
		}

		public static StringResult GetSettings(ESettings settings)
		{
			return settings switch
			{
				ESettings.LightThreshold => StringResult.Success(Service.Settings.LightThreshold.ToString()),
				ESettings.LampToggle => StringResult.Success(Service.Settings.LampToggle.ToString()),
				ESettings.AutomaticMode => StringResult.Success(Service.Settings.AutomaticMode.ToString()),
				ESettings.MorningHour => StringResult.Success(Service.Settings.MorningHour.ToString()),
				ESettings.MotionOffMax => StringResult.Success(Service.Settings.MotionOffMax.ToString()),
				ESettings.MotionOffMin => StringResult.Success(Service.Settings.MotionOffMin.ToString()),
				ESettings.CO2Threshold => StringResult.Success(Service.Settings.Eco2Threshold.ToString()),
				ESettings.TvocThreshold => StringResult.Success(Service.Settings.TvocThreshold.ToString()),
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
				case ESettings.LampToggle:
					Service.Settings.LampToggle = (EStatus)value;
					break;
				case ESettings.CO2Threshold:
					Service.Settings.Eco2Threshold = value;
					break;
				case ESettings.TvocThreshold:
					Service.Settings.TvocThreshold = value;
					break;
				case ESettings.AutomaticMode:
					if (value != (int)EStatus.On && value != (int)EStatus.Off) value = (int)(Service.Settings.AutomaticMode == EStatus.On ? EStatus.Off : EStatus.On);
					Service.Settings.AutomaticMode = (EStatus)value;
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
			Service.Settings.Save();
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
				switch (hardwareInfo)
				{
					case ERaspberryInfo.Location:
						return StringResult.Success(RaspberryHandler.GetNetworkLocation().ToString());
					case ERaspberryInfo.Gateway:
						return StringResult.Success(RaspberryHandler.GetNetworkGateway());
					case ERaspberryInfo.Temperature:
						double temp = RaspberryHandler.ReadCpuTemperature();
						return StringResult.Success(temp.ToString());
					case ERaspberryInfo.Ip:
						string ip = RaspberryHandler.RequestPublicIpv4().Result;
						return StringResult.Success(ip);
					default:
						return StringResult.Failure("Raspberry information not supported");
				}
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
				if (GetPower(EDevice.Computer) == EStatus.Off) return StringResult.Failure("Computer is off");
				bool result = command switch
				{
					EComputerCommand.Shutdown => ComputerHandler.Shutdown(),
					EComputerCommand.Suspend => ComputerHandler.Suspend(),
					EComputerCommand.Notify => ComputerHandler.Notify(args ?? $"Still alive at {Helper.FormatTemperature(RaspberryHandler.ReadCpuTemperature())}"),
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

		public static EStatus GetPower(EDevice device)
		{
			lock (DeviceLock)
			{
				return DeviceHandler.DeviceStatus[device];
			}
		}

		public static StringResult Switch(EDevice device, ESwitchAction trigger, bool automatic = false)
		{
			lock (DeviceLock)
			{
				EStatus? result = device switch
				{
					EDevice.Computer => HandleComputer(trigger), // Check if power supply is off before turning on
					EDevice.Power => HandleComputerSupply(trigger), // Check if computer is off before removing power
					EDevice.Lamp => HandleLamp(trigger, automatic), // Enable temporary manual mode if lamp is switched on manually
					EDevice.Generic => DeviceHandler.PowerGeneric(trigger),
					_ => null
				};
				return !result.HasValue ? StringResult.Failure("Device not supported") : StringResult.Success(result.Value.ToString());
			}
		}

		public static StringResult SwitchRoom(ESwitchAction action)
		{
			lock (DeviceLock)
			{
				if (action == ESwitchAction.On)
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

		private static EStatus HandleLamp(ESwitchAction action, bool automatic)
		{
			if (!automatic) Service.TemporaryManualMode();
			return DeviceHandler.PowerLamp(action);
		}

		private static EStatus HandleComputer(ESwitchAction action)
		{
			switch (action)
			{
				case ESwitchAction.On:
					if (GetPower(EDevice.Power) == EStatus.Off) DeviceHandler.PowerComputerSupply(ESwitchAction.On);
					return DeviceHandler.PowerComputer(ESwitchAction.On);
				case ESwitchAction.Off:
					return DeviceHandler.PowerComputer(ESwitchAction.Off);
				case ESwitchAction.Toggle:
				default:
					return HandleComputer(Helper.ConvertToggle(EDevice.Computer));
			}
		}

		private static EStatus HandleComputerSupply(ESwitchAction action)
		{
			switch (action)
			{
				case ESwitchAction.On:
					return HandleComputer(ESwitchAction.On);
				case ESwitchAction.Off when GetPower(EDevice.Computer) == EStatus.On:
					Task.Run(async () =>
					{
						DeviceHandler.PowerComputer(ESwitchAction.Off);
						await ComputerHandler.CheckForConnection();
						DeviceHandler.PowerComputerSupply(ESwitchAction.Off);
					});
					return EStatus.Off;
				case ESwitchAction.Off:
					return DeviceHandler.PowerComputerSupply(ESwitchAction.Off);
				case ESwitchAction.Toggle:
				default:
					return HandleComputerSupply(Helper.ConvertToggle(EDevice.Power));
			}
		}
	}
}