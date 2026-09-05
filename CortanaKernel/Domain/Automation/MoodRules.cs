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

/// Everything worth worrying about, kept apart so each one is reacted to on its own. A caller that
/// has already shrugged off a silent station should still notice the disk filling up behind it
[Flags]
public enum Worry
{
	None = 0,
	Intruder = 1,
	Silence = 2,
	Service = 4,
	Disk = 8
}

public static class MoodRules
{
	public static readonly TimeSpan AloneAfter = TimeSpan.FromHours(3);

	public static readonly Mood[] Nominal = [Mood.Calm, Mood.Friendly, Mood.Helpful];

	/// The computer being off for this long is what makes an empty desk boring rather than momentary
	public static readonly TimeSpan OffAWhile = TimeSpan.FromMinutes(45);

	private static readonly ActivityCategory[] AtThePc = [ActivityCategory.Coding, ActivityCategory.Browsing, ActivityCategory.Studying];

	/// Which one she reacts to when several land together, worst first
	private static readonly Worry[] Ranked = [Worry.Intruder, Worry.Service, Worry.Silence, Worry.Disk];

	public static bool IsNominal(Mood mood) => Array.IndexOf(Nominal, mood) >= 0;

	private const double QuietLoad = 70;
	private const double FullDisk = 0.9;

	/// The raw conditions, before any damping. A caller may choose to express none of them
	public static Worry Worries(MoodInput input)
	{
		if (input.SleepMode) return Worry.None;

		var worries = Worry.None;

		if (input.WarningActive && input.MotionActive) worries |= Worry.Intruder;
		if (!input.CriticalSourcesOnline) worries |= Worry.Silence;
		if (input.AnyServiceDown) worries |= Worry.Service;
		if (input.DiskUsedFraction >= FullDisk) worries |= Worry.Disk;

		return worries;
	}

	/// The one of them she would speak about
	public static Worry Worst(Worry worries) => Array.Find(Ranked, worry => worries.HasFlag(worry));

	/// What the mood is when she is not (or is no longer) expressing worry
	public static Mood NonWorried(MoodInput input)
	{
		if (input.SleepMode) return Mood.Resting;

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

	/// Explains whichever mood is actually being shown, which a damped Worried may not be, and for a
	/// worry the one cause being reacted to rather than whatever is worst in the room
	public static string Explain(Mood mood, Worry shown, MoodInput input) => mood switch
	{
		Mood.Resting => "sleep mode is active",
		Mood.Worried => shown switch
		{
			Worry.Intruder => "a warning is firing and someone is in the room",
			Worry.Silence => "something stopped reporting",
			Worry.Service => "one of the services is down",
			_ => "the disk is nearly full"
		},
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
