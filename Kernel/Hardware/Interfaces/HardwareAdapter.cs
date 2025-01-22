using Kernel.Hardware.ClientHandlers;
using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;
using Kernel.Software.Utility;

namespace Kernel.Hardware.Interfaces;

public abstract class HardwareAdapter: IHardwareAdapter
{
	static HardwareAdapter()
	{
		Task.Run(ServerHandler.StartListening);
	}
	
	public static double ReadCpuTemperature()
	{
		return RaspberryHandler.ReadCpuTemperature();
	}

	public static bool Ping(string address)
	{
		return Helper.Ping(address);
	}

	public static string GetHardwareInfo(EHardwareInfo hardwareInfo)
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

	public static string GetSensorInfo(ESensor sensor)
	{
		int? val = sensor switch
		{
			ESensor.Light => SensorsHandler.GetLastLightData(),
			ESensor.Temperature => SensorsHandler.GetLastTempData(),
			ESensor.Humidity => SensorsHandler.GetLastHumData(),
			ESensor.Motion => SensorsHandler.GetLastMotionData(),
			_ => null
		};
		
		return val.HasValue ? val.Value.ToString() : "Sensor offline";
	}

	public static string GetSensorInfo(string sensor)
	{
		ESensor? sensorValue = Helper.EnumFromString<ESensor>(sensor);
		return sensorValue.HasValue ? GetSensorInfo(sensorValue.Value) : "Sensor offline";
	}

	public static string CommandRaspberry(ERaspberryOption option)
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
	
	public static string CommandComputer(EComputerCommand command, string? args = null)
	{
		bool result = command switch
		{
			EComputerCommand.Shutdown => ComputerHandler.Shutdown(),
			EComputerCommand.Suspend => ComputerHandler.Suspend(),
			EComputerCommand.Notify => ComputerHandler.Notify(args ?? string.Empty),
			EComputerCommand.Reboot => ComputerHandler.Reboot(),
			EComputerCommand.Command => ComputerHandler.Command(args ?? string.Empty),
			EComputerCommand.SwapOs => ComputerHandler.SwapOs(),
			_ => false
		};
		return result ? "Command executed" : "Command not found";
	}
	
	public static string SwitchDevice(EDevice device, EPowerAction trigger)
	{
		EPower result = device switch
		{
			EDevice.Computer => DeviceHandler.PowerComputer(trigger),
			EDevice.Lamp => DeviceHandler.PowerLamp(trigger),
			EDevice.Power => DeviceHandler.PowerComputerSupply(trigger),
			EDevice.Generic => DeviceHandler.PowerGeneric(trigger),
			_ => throw new CortanaException("Device not supported")
		};
		return $"{device} switched {result}";
	}
	
	public static string SwitchDevice(string device, string trigger)
	{
		EDevice? elementResult = Helper.EnumFromString<EDevice>(device);
		EPowerAction? triggerResult = Helper.EnumFromString<EPowerAction>(trigger);
		if (triggerResult == null) return "Invalid action";
		if (elementResult == null) return "Hardware device not registered";
		return SwitchDevice(elementResult.Value, triggerResult.Value);
	}

	public static void ShutdownServices()
	{
		ComputerHandler.Interrupt();
		SensorsHandler.Interrupt();
		ServerHandler.ShutdownServer();
	}
	
	public static EPower GetDevicePower(EDevice device)
	{
		return DeviceHandler.HardwareStates[device];
	}
}