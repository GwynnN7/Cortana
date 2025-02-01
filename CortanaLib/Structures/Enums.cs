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
	CortanaWeb,
	CortanaDiscord,
	CortanaTelegram
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
	Log,
	Projects
}

public enum EMessageCategory
{
	Urgent,
	Update
}
