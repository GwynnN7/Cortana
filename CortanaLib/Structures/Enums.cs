namespace CortanaLib.Structures;

public enum ERoute
{
	Computer,
	Devices,
	Raspberry,
	Sensors,
	SubFunctions,
	Settings
}

public enum ESubFunctionType
{
	CortanaKernel,
	CortanaTelegram,
	CortanaDiscord,
	CortanaWeb
}

public enum ESubfunctionAction
{
	Start,
	Restart,
	Stop,
	Update
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
	Temp
}

public enum EMessageCategory
{
	Telegram,
	Discord
}
