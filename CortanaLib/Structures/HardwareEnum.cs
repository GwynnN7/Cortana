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
	Command,
	Launch
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
	LampToggle,
	CO2Threshold,
	TvocThreshold,
	AutomaticMode,
	MorningHour,
	NightHour,
	MotionOffMax,
	MotionOffMin,
	ManualModeMinutes,
	LogToWeb,
	LogToTelegram,
	LogToDiscord
}

public static class SettingGroups
{
	public static readonly ESettings[] Logs = [ESettings.LogToWeb, ESettings.LogToTelegram, ESettings.LogToDiscord];

	public static bool IsLog(this ESettings setting) => Logs.Contains(setting);
}

public enum EAutomationState
{
		Automatic,

		Manual,

		Night
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
