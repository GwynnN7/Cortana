namespace Kernel.Hardware.DataStructures;

public enum EDevice
{
	Computer,
	Power,
	Lamp,
	Generic
}

public enum EPower
{
	Off,
	On
}

public enum EPowerAction
{
	On,
	Off,
	Toggle
}

public enum ERaspberryOption
{
	Shutdown,
	Reboot,
	Update
}

public enum ESensorData
{
	Temperature,
	Humidity,
	Light,
	Motion
}

public enum EComputerCommand
{
	Shutdown,
	Suspend,
	Reboot,
	Notify,
	SwapOs,
	Command
}

public enum ELocation
{
	Orvieto,
	Pisa
}

public enum EHardwareInfo
{
	Location,
	Ip,
	Gateway,
	Temperature
}

public enum EControlMode
{
	Manual,
	NightHandler,
	MotionSensor
}

public enum ENotificationPriority
{
	Low,
	High
}