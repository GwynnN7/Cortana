using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

/// Builds the read model
public sealed class SnapshotService(
	DeviceService devices,
	SensorService sensors,
	AutomationService automation,
	SettingsService settings,
	ServiceControlService services,
	PluginService plugins,
	ActivityRegistry activity)
{
	private readonly Lock _flavourGate = new();
	private CortanaLib.Primitives.Mood _flavour = CortanaLib.Primitives.Mood.Calm;
	private bool _nominal;

	// A worrying condition does not hold Worried for as long as it lasts. Each one that turns up only
	// has a chance of being reacted to at all, and when it is, for a bounded burst: a station left off
	// all day should not read as permanent alarm, because the reaction is real but it does not linger.
	// Each cause is judged on its own, so shrugging one off never makes her deaf to the next
	private static readonly TimeSpan WorryMin = TimeSpan.FromMinutes(30);
	private static readonly TimeSpan WorryMax = TimeSpan.FromHours(2);
	private const double WorryChance = 0.6;

	private Worry _worries;
	private Worry _shown;
	private DateTimeOffset? _shownUntil;

	/// Decides the mood to show and the sentence behind it, atomically, so the two can never mismatch
	private (CortanaLib.Primitives.Mood Mood, string Reason) Evaluate(MoodInput input)
	{
		lock (_flavourGate)
		{
			Worry worries = MoodRules.Worries(input);
			Worry appeared = worries & ~_worries;
			_worries = worries;

			// She stops expressing a worry once the cause is gone, or once she has expressed it enough
			if (!worries.HasFlag(_shown) || (_shownUntil is { } until && input.Now >= until))
			{
				_shown = Worry.None;
				_shownUntil = null;
			}

			if (_shown == Worry.None && appeared != Worry.None && Random.Shared.NextDouble() < WorryChance)
			{
				_shown = MoodRules.Worst(appeared);
				_shownUntil = input.Now + WorryMin + (WorryMax - WorryMin) * Random.Shared.NextDouble();
			}

			CortanaLib.Primitives.Mood decided = _shown == Worry.None
				? MoodRules.NonWorried(input)
				: CortanaLib.Primitives.Mood.Worried;

			string reason = MoodRules.Explain(decided, _shown, input);

			if (!MoodRules.IsNominal(decided))
			{
				_nominal = false;
				return (decided, reason);
			}

			if (!_nominal)
			{
				_nominal = true;
				_flavour = MoodRules.Nominal[Random.Shared.Next(MoodRules.Nominal.Length)];
			}

			return (_flavour, reason);
		}
	}

	public async Task<CortanaSnapshot> Build(CancellationToken token = default)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		IReadOnlyList<ServiceView> running = await services.All(token);

		MoodInput situation = Situation(running, now);
		(CortanaLib.Primitives.Mood mood, string reason) = Evaluate(situation);

		return new CortanaSnapshot(
			now,
			mood,
			reason,
			sensors.Sources(),
			devices.All(),
			sensors.All(),
			automation.View(),
			settings.All(),
			await devices.HostInformation(token),
			running,
			plugins.All(),
			activity.Current);
	}

	public MoodInput Situation(IReadOnlyList<ServiceView> running, DateTimeOffset now)
	{
		AutomationView view = automation.View();

		return new MoodInput(
			view.SleepMode,
			view.WarningActive,
			view.CriticalSourcesOnline,
			devices.ComputerConnected,
			running.Any(service => !service.Running),
			sensors.Value("pc_cpu") ?? 0,
			(sensors.Value("pi_disk") ?? 0) / 100,
			view.MotionActive,
			view.LastMotionAt,
			activity.Current?.Category,
			activity.Current?.Fullscreen ?? false,
			activity.Current is { } busy && (busy.Fullscreen || busy.Category == ActivityCategory.Gaming),
			sensors.SeenAt(SourceIds.Computer),
			now);
	}

	public async Task<Mood> Mood(CancellationToken token = default)
	{
		return Evaluate(Situation(await services.All(token), DateTimeOffset.Now)).Mood;
	}

	public async Task<string> MoodReason(CancellationToken token = default)
	{
		return Evaluate(Situation(await services.All(token), DateTimeOffset.Now)).Reason;
	}

	public string Doing()
	{
		DesktopActivity? current = activity.Current;
		if (current is null) return "";

		string focus = current switch
		{
			{ Locked: true } => "gwynn7 is away, the desk is locked.",
			{ Category: ActivityCategory.Gaming, Fullscreen: true } => "gwynn7 is in a fullscreen game, so keep it short and do not interrupt.",
			{ Category: ActivityCategory.Media, Fullscreen: true } => "gwynn7 is watching something fullscreen, so keep it short.",
			{ Category: ActivityCategory.Idle } => "",
			{ Category: var category } => $"gwynn7 is at the computer, {category.ToString().ToLowerInvariant()}."
		};

		if (current.Playing is not { Paused: false } track) return focus;

		string music = track switch
		{
			{ Title.Length: > 0, Artist.Length: > 0 } => $" {track.Title} by {track.Artist} is playing.",
			{ Title.Length: > 0 } => $" {track.Title} is playing.",
			_ => " Music is playing."
		};

		return focus + music;
	}

	public AutomationDiagnostics Diagnostics(IReadOnlyList<NotificationEntry> recent) => new(
		automation.View(),
		automation.Engine.LastDecision,
		sensors.Sources(),
		devices.All(),
		sensors.All(),
		settings.All(),
		automation.Engine.Decisions,
		recent);
}
