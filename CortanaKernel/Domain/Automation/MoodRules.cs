using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

public readonly record struct MoodInput(
	bool SleepMode,
	bool AirQualityWarning,
	bool StationOnline,
	bool ComputerConnected,
	bool AnyServiceDown,
	double MachineLoad,
	double DiskUsedFraction,
	bool MotionActive,
	DateTimeOffset? LastMotionAt,
	ActivityCategory? Activity,
	bool Fullscreen,
	bool DesktopBusy,
	DateTimeOffset Now);

public static class MoodRules
{
	public static readonly TimeSpan AloneAfter = TimeSpan.FromHours(3);

	public static readonly Mood[] Nominal = [Mood.Calm, Mood.Friendly, Mood.Helpful, Mood.Happy];

	public static bool IsNominal(Mood mood) => Array.IndexOf(Nominal, mood) >= 0;

	private const double QuietLoad = 70;
	private const double FullDisk = 0.9;

	public static Mood Decide(MoodInput input)
	{
		if (input.SleepMode) return Mood.Resting;

		if (input.AirQualityWarning && input.MotionActive) return Mood.Worried;
		if (!input.StationOnline || input.AnyServiceDown || input.DiskUsedFraction >= FullDisk) return Mood.Worried;

		if (input.DesktopBusy) return Mood.Watching;
		if (input.ComputerConnected && input.MachineLoad >= QuietLoad) return Mood.Watching;

		if (input.Activity is ActivityCategory.Away or ActivityCategory.Locked && !input.MotionActive) return Mood.Alone;
		if (!input.MotionActive && !input.ComputerConnected &&
			(input.LastMotionAt is null || input.Now - input.LastMotionAt.Value >= AloneAfter))
			return Mood.Alone;

		return Mood.Calm;
	}

	public static string Explain(MoodInput input) => Decide(input) switch
	{
		Mood.Resting => "sleep mode is active",
		Mood.Worried when input.AirQualityWarning && input.MotionActive => "the air is bad and someone is in the room",
		Mood.Worried when !input.StationOnline => "the station is not reporting",
		Mood.Worried when input.AnyServiceDown => "one of the services is down",
		Mood.Worried => "the disk is nearly full",
		Mood.Watching when input.Activity == ActivityCategory.Gaming => "a game is running",
		Mood.Watching when input.Fullscreen => "something is playing fullscreen",
		Mood.Watching => $"the computer is loaded at {input.MachineLoad:F0}%",
		Mood.Alone when input.Activity is ActivityCategory.Away or ActivityCategory.Locked => "the desk is locked and empty",
		Mood.Alone => "nobody has been here for hours and the computer is off",
		_ => "everything is nominal"
	};
}
