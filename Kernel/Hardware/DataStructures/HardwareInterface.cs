namespace Kernel.Hardware.DataStructures;

internal interface IHardwareAdapter
{
	internal static abstract void SubscribeNotification(Action<string> action, ENotificationPriority priority);
	internal static abstract NetworkData GetNetworkData();
	internal static abstract Settings GetSettings();
	internal static abstract EPower GetDevicePower(EDevice device);
	internal static abstract bool Ping(string address);
	internal static abstract double ReadCpuTemperature();
	internal static abstract string GetHardwareInfo(EHardwareInfo hardwareInfo);
	internal static abstract string GetSensorInfo(ESensor sensor);
	internal static abstract string CommandRaspberry(ERaspberryOption option);
	internal static abstract string CommandComputer(EComputerCommand command, string? args = null);
	internal static abstract string SwitchDevice(EDevice device, EPowerAction trigger);
	internal static abstract void ShutdownServices();
}