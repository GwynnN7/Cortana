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

public enum ELocation
{
	Orvieto,
	Pisa
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
	Motion,
	Humidity,
	CO2,
	Tvoc
}

public enum ESettings
{
	LightThreshold,
	CO2Threshold,
	TvocThreshold,
	AutomaticMode,
	MorningHour,
	MotionOffMax,
	MotionOffMin
}

public enum EStatus
{
	Off = 0,
	On = 1
}

public enum ESwitchAction
{
	Off,
	On,
	Toggle
}