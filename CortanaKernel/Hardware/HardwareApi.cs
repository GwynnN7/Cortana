global using StringResult = CortanaLib.Structures.Result<string, string>;
using System.Globalization;
using CortanaKernel.Hardware.Devices;
using CortanaKernel.Hardware.SocketHandler;
using CortanaKernel.Hardware.Utility;
using CortanaKernel.Kernel;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware;

public static class HardwareApi
{
	private static readonly Lock DeviceLock = new();
	private static readonly SemaphoreSlim ComputerGate = new(1, 1);

	public static void InitializeHardware()
	{
		DeviceHandler.LoadDevices();
		ServerHandler.Initialize();
		AutomationService.Start();
		ScheduleService.Start();
	}

	public static void ShutdownService()
	{
		ComputerHandler.Interrupt();
		SensorsHandler.Interrupt();
		ServerHandler.ShutdownServer();
		AutomationService.Interrupt();
		ScheduleService.Interrupt();
		DeviceHandler.Shutdown();
	}

	public static class Sensors
	{
		public static bool IsStationOnline => SensorsHandler.IsOnline;

				public static EAutomationState AutomationState => AutomationService.State;

		public static bool LogDestinationEnabled(ESettings destination) => destination switch
		{
			ESettings.LogToWeb => AutomationService.Settings.LogToWeb == EStatus.On,
			ESettings.LogToTelegram => AutomationService.Settings.LogToTelegram == EStatus.On,
			ESettings.LogToDiscord => AutomationService.Settings.LogToDiscord == EStatus.On,
			_ => false
		};

		public static StringResult GetData(ESensor sensor)
		{
			switch (sensor)
			{
				case ESensor.Temperature:
					double? temp = SensorsHandler.GetRoomTemperature();
					if (temp.HasValue) return StringResult.Success(Math.Round(temp.Value, 1).ToString(CultureInfo.InvariantCulture));
					break;
				case ESensor.Light:
					int? light = SensorsHandler.GetRoomLightLevel();
					if (light.HasValue) return StringResult.Success(light.Value.ToString(CultureInfo.InvariantCulture));
					break;
				case ESensor.Humidity:
					double? humidity = SensorsHandler.GetRoomHumidity();
					if (humidity.HasValue) return StringResult.Success(Math.Round(humidity.Value, 1).ToString(CultureInfo.InvariantCulture));
					break;
				case ESensor.CO2:
					int? co2 = SensorsHandler.GetRoomEco2();
					if (co2.HasValue) return StringResult.Success(co2.Value.ToString(CultureInfo.InvariantCulture));
					break;
				case ESensor.Tvoc:
					int? tvoc = SensorsHandler.GetRoomTvoc();
					if (tvoc.HasValue) return StringResult.Success(tvoc.Value.ToString(CultureInfo.InvariantCulture));
					break;
				case ESensor.Motion:
					EStatus? motion = SensorsHandler.GetMotionDetected();
					if (motion.HasValue) return StringResult.Success((motion.Value == EStatus.On).ToString().ToLowerInvariant());
					break;
			}
			return StringResult.Failure("Sensor offline");
		}

		public static IReadOnlyList<SensorResponse> GetAllData()
		{
			return Enum.GetValues<ESensor>()
				.Select(sensor => GetData(sensor).Match(
					value => new SensorResponse(sensor.ToString(), value, Helper.UnitFor(sensor)),
					_ => new SensorResponse(sensor.ToString(), "", Helper.UnitFor(sensor))))
				.ToList();
		}

		public static StringResult GetSettings(ESettings settings)
		{
			return settings switch
			{
				ESettings.LightThreshold => StringResult.Success(AutomationService.Settings.LightThreshold.ToString()),
				ESettings.LampToggle => StringResult.Success(AutomationService.Settings.LampToggle.ToString()),
				ESettings.AutomaticMode => StringResult.Success(AutomationService.Settings.AutomaticMode.ToString()),
				ESettings.MorningHour => StringResult.Success(AutomationService.Settings.MorningHour.ToString()),
				ESettings.MotionOffMax => StringResult.Success(AutomationService.Settings.MotionOffMax.ToString()),
				ESettings.MotionOffMin => StringResult.Success(AutomationService.Settings.MotionOffMin.ToString()),
				ESettings.NightHour => StringResult.Success(AutomationService.Settings.NightHour.ToString()),
				ESettings.ManualModeMinutes => StringResult.Success(AutomationService.Settings.ManualModeMinutes.ToString()),
				ESettings.LogToWeb => StringResult.Success(AutomationService.Settings.LogToWeb.ToString()),
				ESettings.LogToTelegram => StringResult.Success(AutomationService.Settings.LogToTelegram.ToString()),
				ESettings.LogToDiscord => StringResult.Success(AutomationService.Settings.LogToDiscord.ToString()),
				ESettings.CO2Threshold => StringResult.Success(AutomationService.Settings.Eco2Threshold.ToString()),
				ESettings.TvocThreshold => StringResult.Success(AutomationService.Settings.TvocThreshold.ToString()),
				_ => StringResult.Failure("Settings not found")
			};
		}

		public static IReadOnlyList<SettingsResponse> GetAllSettings()
		{
			return Enum.GetValues<ESettings>()
				.Select(setting => GetSettings(setting).Match(
					value => new SettingsResponse(setting.ToString(), value),
					_ => new SettingsResponse(setting.ToString(), "")))
				.ToList();
		}

		public static StringResult SetSettings(ESettings settings, int value)
		{
			switch (settings)
			{
				case ESettings.LightThreshold:
					AutomationService.Settings.LightThreshold = value;
					break;
				case ESettings.LampToggle:
					AutomationService.Settings.LampToggle = ToStatus(value, AutomationService.Settings.LampToggle);
					break;
				case ESettings.CO2Threshold:
					AutomationService.Settings.Eco2Threshold = value;
					break;
				case ESettings.TvocThreshold:
					AutomationService.Settings.TvocThreshold = value;
					break;
				case ESettings.AutomaticMode:
					AutomationService.Settings.AutomaticMode = ToStatus(value, AutomationService.Settings.AutomaticMode);
					
					if (AutomationService.Settings.AutomaticMode == EStatus.On) AutomationService.ClearManualHold();
					break;
				case ESettings.MorningHour:
					AutomationService.Settings.MorningHour = value;
					break;
				case ESettings.MotionOffMax:
					AutomationService.Settings.MotionOffMax = value;
					break;
				case ESettings.MotionOffMin:
					AutomationService.Settings.MotionOffMin = value;
					break;
				case ESettings.NightHour:
					AutomationService.Settings.NightHour = value;
					break;
				case ESettings.ManualModeMinutes:
					AutomationService.Settings.ManualModeMinutes = value;
					break;
				case ESettings.LogToWeb:
					AutomationService.Settings.LogToWeb = ToStatus(value, AutomationService.Settings.LogToWeb);
					break;
				case ESettings.LogToTelegram:
					AutomationService.Settings.LogToTelegram = ToStatus(value, AutomationService.Settings.LogToTelegram);
					break;
				case ESettings.LogToDiscord:
					AutomationService.Settings.LogToDiscord = ToStatus(value, AutomationService.Settings.LogToDiscord);
					break;
				default:
					return StringResult.Failure("Settings not found");
			}

			AutomationService.Settings.Save();
			SystemEvents.Notify();
			return GetSettings(settings);
		}

		private static EStatus ToStatus(int value, EStatus current)
		{
			if (value == (int)EStatus.On) return EStatus.On;
			if (value == (int)EStatus.Off) return EStatus.Off;
			return current == EStatus.On ? EStatus.Off : EStatus.On;
		}
	}

	public static class Raspberry
	{
		public static StringResult Command(ERaspberryCommand option)
		{
			switch (option)
			{
				case ERaspberryCommand.Shutdown:
					RaspberryHandler.Shutdown();
					return StringResult.Success("Shutting down");
				case ERaspberryCommand.Reboot:
					RaspberryHandler.Reboot();
					return StringResult.Success("Rebooting");
				default:
					return StringResult.Failure("Command not found");
			}
		}

		public static Task<StringResult> RunCommand(string command) => RaspberryHandler.RunShellCommand(command);

		public static async Task<StringResult> GetHardwareInfo(ERaspberryInfo hardwareInfo)
		{
			return hardwareInfo switch
			{
				ERaspberryInfo.Location => StringResult.Success(RaspberryHandler.GetNetworkLocation().ToString()),
				ERaspberryInfo.Gateway => StringResult.Success(RaspberryHandler.GetNetworkGateway()),
				ERaspberryInfo.Temperature => StringResult.Success(Math.Round(RaspberryHandler.ReadCpuTemperature(), 1).ToString(CultureInfo.InvariantCulture)),
				ERaspberryInfo.Ip => StringResult.Success(await RaspberryHandler.RequestPublicIpv4()),
				_ => StringResult.Failure("Raspberry information not supported")
			};
		}

		public static async Task<IReadOnlyList<SensorResponse>> GetAllHardwareInfo()
		{
			var results = new List<SensorResponse>();
			foreach (ERaspberryInfo info in Enum.GetValues<ERaspberryInfo>())
			{
				StringResult result = await GetHardwareInfo(info);
				results.Add(new SensorResponse(info.ToString(), result.Match(value => value, _ => ""), Helper.UnitFor(info)));
			}
			return results;
		}
	}

	public static class Devices
	{
		public static void EnterSleepMode() => AutomationService.EnterSleepMode();

		public static async Task<StringResult> CommandComputer(EComputerCommand command, string? args = null)
		{
			await ComputerGate.WaitAsync();
			try
			{
				if (GetPower(EDevice.Computer) == EStatus.Off) return StringResult.Failure("Computer is off");

				if (command == EComputerCommand.Command) return await ComputerHandler.RunCommand(args ?? "dir");

				bool result = command switch
				{
					EComputerCommand.Shutdown => ComputerHandler.Shutdown(),
					EComputerCommand.Suspend => ComputerHandler.Suspend(),
					EComputerCommand.Notify => ComputerHandler.Notify(args ?? $"Still alive at {Helper.FormatTemperature(RaspberryHandler.ReadCpuTemperature())}"),
					EComputerCommand.Reboot => ComputerHandler.Reboot(),
					EComputerCommand.System => ComputerHandler.SwitchOs(),
					_ => false
				};

				SystemEvents.Notify();
				return result ? StringResult.Success("Command executed") : StringResult.Failure("Command not found");
			}
			finally
			{
				ComputerGate.Release();
			}
		}

		public static EStatus GetPower(EDevice device)
		{
			lock (DeviceLock) return DeviceHandler.DeviceStatus[device];
		}

		public static IReadOnlyList<DeviceResponse> GetAllPower()
		{
			lock (DeviceLock)
			{
				return Enum.GetValues<EDevice>()
					.Select(device => new DeviceResponse(device.ToString(), DeviceHandler.DeviceStatus[device].ToString()))
					.ToList();
			}
		}

		public static StringResult Switch(EDevice device, ESwitchAction trigger, bool automatic = false)
		{
			lock (DeviceLock)
			{
				EStatus? result = device switch
				{
					EDevice.Computer => HandleComputer(trigger), 
					EDevice.Power => HandleComputerSupply(trigger), 
					EDevice.Lamp => HandleLamp(trigger, automatic), 
					EDevice.Generic => DeviceHandler.PowerGeneric(trigger),
					_ => null
				};
				if (result.HasValue) SystemEvents.Notify();
				return !result.HasValue ? StringResult.Failure("Device not supported") : StringResult.Success(result.Value.ToString());
			}
		}

		public static StringResult SwitchRoom(ESwitchAction action)
		{
			lock (DeviceLock)
			{
				if (action != ESwitchAction.On) Switch(EDevice.Lamp, action);
				else if (SensorsHandler.GetRoomLightLevel().GetValueOrDefault(0) <= AutomationService.Settings.LightThreshold) Switch(EDevice.Lamp, action);

				StringResult powerResult = Switch(EDevice.Power, action);
				return powerResult.IsOk ? StringResult.Success(action.ToString()) : StringResult.Failure("Devices failed to switch");
			}
		}

		private static EStatus HandleLamp(ESwitchAction action, bool automatic)
		{
			if (!automatic) AutomationService.TemporaryManualMode();
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
