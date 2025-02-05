namespace CortanaLib.Structures;

public enum ERoute
{
	Computer,
	Device,
	Raspberry,
	Sensor,
	SubFunction,
	Settings
}

public enum ESubFunctionType
{
	CortanaKernel,
	CortanaWeb,
	CortanaDiscord,
	CortanaTelegram
}

public enum ESubfunctionAction
{
	Restart,
	Reboot,
	Update,
	Stop
}

public enum ETimerType
{
	Utility,
	Discord,
	Telegram
}

public enum ETimerLoop
{
	No,
	Interval,
	Daily,
	Weekly
}

public enum EVideoQuality
{
	BestVideo,
	BestAudio,
	Balanced
}

public enum EDirType
{
	Config,
	Storage,
	Log
}

public enum EMessageCategory
{
	Urgent,
	Update
}
