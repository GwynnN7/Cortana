namespace CortanaLib.Primitives;

public enum PowerState
{
	Off = 0,
	On = 1
}

public enum SwitchAction
{
	Off,
	On,
	Toggle
}

public enum Mood
{
	Calm,
	Friendly,
	Helpful,
	Happy,
	Watching,
	Worried,
	Resting,
	Bored,
	Alone
}

/// What automation is doing
public enum AutomationStatus
{
	Active,
	Holding,
	Idle,
	Off
}

public enum TimeContext
{
	Day,
	Night
}

public enum Location
{
	Orvieto,
	Pisa
}

public enum RaspberryInfo
{
	Temperature,
	Location,
	Gateway,
	PublicIp
}

public enum RaspberryCommand
{
	Shutdown,
	Reboot,
	RunShellCommand
}

public enum ComputerCommand
{
	Shutdown,
	Suspend,
	Reboot,
	Notify,
	BootIntoOtherOperatingSystem,
	RunShellCommand,
	LaunchApplication,
	CloseApplication,
	SetActivityDetail
}

public enum ServiceId
{
	Kernel,
	Telegram,
	Discord,
	Web
}

public enum ServiceAction
{
	Start,
	Stop,
	Restart,
	Update
}

public enum NotificationLevel
{
	Info,
	Warning,
	Alert
}

public enum NotificationSource
{
	Kernel,
	Devices,
	Computer,
	Sensors,
	Motion,
	Warnings,
	Automation,
	Sleep,
	Schedule,
	Service,
	Ai,
	Cortana
}

public enum NotificationChannel
{
	Web,
	Telegram,
	Discord
}

/// Every persisted runtime setting the user can change
public enum SettingKey
{
	AutomationEnabled,
	MorningHour,
	NightHour,
	MotionTimeoutSeconds,
	ManualOverrideMinutes,
	SleepManualOverrideMinutes,
	SleepHoldMinutes,
	SleepEntryDelayMinutes,
	DaySleepMinutes,
	ComputerShutdownGraceSeconds,
	NotifyWeb,
	NotifyTelegram,
	NotifyDiscord,
	SleepEnabled,
	WarningsEnabled,
	NotesEnabled,
	MemoryEnabled,
	HistoryEnabled,
	WrapupEnabled
}

public enum VideoQuality
{
	BestVideo,
	BestAudio,
	Balanced
}

/// Who asked for something, and through which surface
public enum CommandActor
{
	User,
	System
}

public enum CommandSurface
{
	Web,
	Telegram,
	Discord,
	Desktop,
	Api,
	Automation,
	Scheduler,
	Startup,
	Internal
}
