using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

public readonly record struct MoodInput(
	bool SleepMode,
	bool WarningActive,
	bool CriticalSourcesOnline,
	bool ComputerConnected,
	bool AnyServiceDown,
	double MachineLoad,
	double DiskUsedFraction,
	bool MotionActive,
	DateTimeOffset? LastMotionAt,
	ActivityCategory? Activity,
	bool Fullscreen,
	bool DesktopBusy,
	DateTimeOffset? ComputerSeenAt,
	DateTimeOffset Now);

public static class MoodRules
{
	public static readonly TimeSpan AloneAfter = TimeSpan.FromHours(3);

	public static readonly Mood[] Nominal = [Mood.Calm, Mood.Friendly, Mood.Helpful];

	/// The computer being off for this long is what makes an empty desk boring rather than momentary
	public static readonly TimeSpan OffAWhile = TimeSpan.FromMinutes(45);

	private static readonly ActivityCategory[] AtThePc = [ActivityCategory.Coding, ActivityCategory.Browsing, ActivityCategory.Studying];

	public static bool IsNominal(Mood mood) => Array.IndexOf(Nominal, mood) >= 0;

	private const double QuietLoad = 70;
	private const double FullDisk = 0.9;

	public static Mood Decide(MoodInput input) => IsWorrying(input) ? Mood.Worried : NonWorried(input);

	/// The raw condition, before any damping. A caller may choose not to express this as Worried
	public static bool IsWorrying(MoodInput input) =>
		(input.WarningActive && input.MotionActive)
		|| !input.CriticalSourcesOnline
		|| input.AnyServiceDown
		|| input.DiskUsedFraction >= FullDisk;

	/// What the mood would be if she is not (or is no longer) expressing worry
	public static Mood NonWorried(MoodInput input)
	{
		if (input.DesktopBusy || input.Fullscreen) return Mood.Watching;
		if (input.ComputerConnected && input.MachineLoad >= QuietLoad) return Mood.Watching;

		if (input.ComputerConnected && input.Activity is { } doing && AtThePc.Contains(doing)) return Mood.Happy;

		if (Away(input) && !input.MotionActive &&
			(input.LastMotionAt is null || input.Now - input.LastMotionAt.Value >= AloneAfter))
			return Mood.Alone;

		if (Away(input)) return Mood.Bored;

		return Mood.Calm;
	}

	/// Nobody is using the computer: either the desk is locked, or the machine has been off a while
	private static bool Away(MoodInput input) =>
		input.Activity is ActivityCategory.Away or ActivityCategory.Locked
		|| (!input.ComputerConnected &&
			(input.ComputerSeenAt is null || input.Now - input.ComputerSeenAt.Value >= OffAWhile));

	/// Explains whichever mood is actually being shown, which a damped Worried may not be
	public static string Explain(Mood mood, MoodInput input) => mood switch
	{
		Mood.Resting => "sleep mode is active",
		Mood.Worried when input.WarningActive && input.MotionActive => "a warning is firing and someone is in the room",
		Mood.Worried when !input.CriticalSourcesOnline => "something stopped reporting",
		Mood.Worried when input.AnyServiceDown => "one of the services is down",
		Mood.Worried => "the disk is nearly full",
		Mood.Watching when input.Activity == ActivityCategory.Gaming => "a game is running",
		Mood.Watching when input.Fullscreen => "something is playing fullscreen",
		Mood.Watching => $"the computer is loaded at {input.MachineLoad:F0}%",
		Mood.Happy => $"someone is at the desk, {input.Activity.ToString()?.ToLowerInvariant()}",
		Mood.Alone when input.Activity is ActivityCategory.Away or ActivityCategory.Locked => "the desk is locked and empty",
		Mood.Alone => "nobody has been here for hours and the computer is off",
		Mood.Bored when input.Activity is ActivityCategory.Away or ActivityCategory.Locked => "the desk is locked, but someone is around",
		Mood.Bored => "the computer has been off a while",
		_ => "everything is nominal"
	};
}
