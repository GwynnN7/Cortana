namespace CortanaLib.Structures;

public enum EDevice
{
	Lamp,
	Computer,
	Power,
	Generic
}

public enum ERaspberryInfo
{
	Temperature,
	Location,
	Gateway,
	Ip
}

public enum ERaspberryCommand
{
	Shutdown,
	Reboot,
	Command
}

public enum EComputerCommand
{
	Shutdown,
	Suspend,
	Reboot,
	Notify,
	System,
	Command
}

public enum ESensor
{
	Temperature,
	Light,
	Motion
}

public enum ESettings
{
	LightThreshold,
	AutomaticMode,
	MorningHour,
	MotionOffMax,
	MotionOffMin
}

public enum EMotionDetection
{
	Off = 0,
	On = 1
}

public enum EPowerStatus
{
	Off,
	On
}

public enum EPowerAction
{
	Off,
	On,
	Toggle
}

public enum ELocation
{
	Orvieto,
	Pisa
}