using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Hardware.Structures;

public class Settings
{
	private const int MaxMotionSeconds = 3600;

	private static readonly string FilePath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Settings.json");

	public int LightThreshold { get; set; } = 60;
	public EStatus LampToggle { get; set; }
	public int Eco2Threshold { get; set; } = 1000;
	public int TvocThreshold { get; set; } = 250;

		public EStatus AutomaticMode
	{
		get;
		set => field = value == EStatus.On ? EStatus.On : EStatus.Off;
	} = EStatus.On;

		public int MorningHour
	{
		get;
		set => field = Math.Clamp(value, 0, 23);
	} = 9;

		public int NightHour
	{
		get;
		set => field = Math.Clamp(value, 0, 23);
	} = 23;

		public int MotionOffMin
	{
		get;
		set => field = Math.Clamp(value, 0, MaxMotionSeconds);
	} = 1;

		public int MotionOffMax
	{
		get;
		set => field = Math.Clamp(value, 0, MaxMotionSeconds);
	} = 30;

		public int ManualModeMinutes
	{
		get;
		set => field = Math.Clamp(value, 1, 720);
	} = 15;

		public EStatus LogToWeb
	{
		get;
		set => field = value == EStatus.On ? EStatus.On : EStatus.Off;
	} = EStatus.On;

	public EStatus LogToTelegram
	{
		get;
		set => field = value == EStatus.On ? EStatus.On : EStatus.Off;
	} = EStatus.Off;

	public EStatus LogToDiscord
	{
		get;
		set => field = value == EStatus.On ? EStatus.On : EStatus.Off;
	} = EStatus.Off;

	private void Normalize()
	{
		if (MotionOffMin > MotionOffMax) (MotionOffMin, MotionOffMax) = (MotionOffMax, MotionOffMin);
	}

	public void Save()
	{
		Normalize();
		this.Serialize().Dump(FilePath);
	}

	public static Settings Load()
	{
		Settings settings = FilePath.Load<Settings>();
		settings.Normalize();
		return settings;
	}
}
