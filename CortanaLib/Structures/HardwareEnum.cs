namespace CortanaLib.Structures;

public enum EDevice
{
	Computer,
	Power,
	Lamp,
	Generic
}

public enum ESubFunctionType
{
	CortanaWeb,
	CortanaDiscord,
	CortanaTelegram
}

public enum ERaspberryInfo
{
	Location,
	Ip,
	Gateway,
	Temperature,
	ApiPort
}

public enum ERaspberryCommand
{
	Shutdown,
	Reboot,
	Update
}

public enum EComputerCommand
{
	Shutdown,
	Suspend,
	Reboot,
	Notify,
	Swapos,
	Command
}

public enum ESensor
{
	Temperature,
	Light,
	Motion
}

public enum ESensorSettings
{
	LightThreshold,
	LimitControlMode
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
	On,
	Off,
	Toggle
}

public enum ELocation
{
	Orvieto,
	Pisa
}