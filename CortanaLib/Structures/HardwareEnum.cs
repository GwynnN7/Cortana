namespace CortanaLib.Structures;

public enum EDevice
{
	Computer,
	Power,
	Lamp,
	Generic
}

public enum ERaspberryInfo
{
	Location,
	Ip,
	Gateway,
	Temperature
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
	MotionDetection,
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