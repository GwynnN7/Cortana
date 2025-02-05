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
	LimitMode,
	ControlMode,
	MorningHour,
	MotionOffMax,
	MotionOffMin
}

public enum EControlMode
{
	Manual = 1,
	Night = 2,
	Automatic = 3
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