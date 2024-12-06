namespace Kernel.Hardware.Utility;

public enum EDevice
{
	Computer,
	Power,
	Lamp,
	Generic
}

public enum EPower
{
	On,
	Off
}

public enum EPowerAction
{
	On,
	Off,
	Toggle
}

public enum EPowerOption
{
	Shutdown,
	Reboot
}

public enum EComputerCommand
{
	Reboot,
	Notify,
	Shell
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