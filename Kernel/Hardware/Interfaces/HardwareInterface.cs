using Kernel.Hardware.DataStructures;
using Kernel.Hardware.Utility;

namespace Kernel.Hardware.Interfaces;

public interface IHardwareAdapter
{
	public static abstract double ReadCpuTemperature();
	public static abstract bool Ping(string address);
	public static abstract string GetHardwareInfo(EHardwareInfo hardwareInfo);
	public static abstract string GetSensorInfo(ESensor sensor);
	public static abstract string GetSensorInfo(string sensor);
	public static abstract string CommandRaspberry(ERaspberryOption option);
	public static abstract string CommandComputer(EComputerCommand command, string? args = null);
	public static abstract string SwitchDevice(EDevice device, EPowerAction trigger);
	public static abstract string SwitchDevice(string device, string trigger);
	public static abstract EPower GetDevicePower(EDevice device);
	public static abstract void ShutdownServices();
}