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

public enum ESensor
{
	Temperature,
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
	Manual = 0,
	Night = 1,
	Automatic = 2
}

public enum ENotificationPriority
{
	Low,
	High
}