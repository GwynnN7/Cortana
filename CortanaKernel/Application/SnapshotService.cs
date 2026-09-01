using CortanaKernel.Domain.Activity;
using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Metrics;
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
	MetricsRegistry metrics,
	ActivityRegistry activity)
{
	private readonly Lock _flavourGate = new();
	private CortanaLib.Primitives.Mood _flavour = CortanaLib.Primitives.Mood.Calm;
	private bool _nominal;

	private CortanaLib.Primitives.Mood Settle(CortanaLib.Primitives.Mood decided)
	{
		lock (_flavourGate)
		{
			if (!MoodRules.IsNominal(decided))
			{
				_nominal = false;
				return decided;
			}

			if (!_nominal)
			{
				_nominal = true;
				_flavour = MoodRules.Nominal[Random.Shared.Next(MoodRules.Nominal.Length)];
			}

			return _flavour;
		}
	}

	public async Task<CortanaSnapshot> Build(CancellationToken token = default)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		IReadOnlyList<ServiceView> running = await services.All(token);
		MetricsView? computer = metrics.Computer(now);
		MetricsView raspberry = metrics.Raspberry(now);

		MoodInput situation = Situation(running, computer, raspberry, now);

		return new CortanaSnapshot(
			now,
			Settle(MoodRules.Decide(situation)),
			MoodRules.Explain(situation),
			devices.All(),
			sensors.All(),
			automation.View(),
			settings.All(),
			await devices.HostInformation(token),
			running,
			computer,
			raspberry,
			activity.Current);
	}

	public MoodInput Situation(IReadOnlyList<ServiceView> running, MetricsView? computer, MetricsView raspberry, DateTimeOffset now)
	{
		AutomationView view = automation.View();

		return new MoodInput(
			view.SleepMode,
			view.AirQualityWarning,
			view.StationOnline,
			devices.ComputerConnected,
			running.Any(service => !service.Running),
			computer is { Stale: false } ? computer.CpuLoad : 0,
			raspberry.DiskTotal > 0 ? raspberry.DiskUsed / raspberry.DiskTotal : 0,
			view.MotionActive,
			view.LastMotionAt,
			activity.Current?.Category,
			activity.Current?.Fullscreen ?? false,
			activity.Current is { } busy && (busy.Fullscreen || busy.Category == ActivityCategory.Gaming),
			now);
	}

	public async Task<Mood> Mood(CancellationToken token = default)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		return Settle(MoodRules.Decide(Situation(await services.All(token), metrics.Computer(now), metrics.Raspberry(now), now)));
	}

	public async Task<string> MoodReason(CancellationToken token = default)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		return MoodRules.Explain(Situation(await services.All(token), metrics.Computer(now), metrics.Raspberry(now), now));
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
		devices.All(),
		sensors.All(),
		settings.All(),
		automation.Engine.Decisions,
		recent);
}
