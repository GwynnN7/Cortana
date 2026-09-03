using CortanaLib.Primitives;

namespace CortanaLib.Contracts;

/// Coherent read model of everything Cortana currently believes
public sealed record CortanaSnapshot(
	DateTimeOffset Timestamp,
	Mood Mood,
	string MoodReason,
	IReadOnlyList<SourceView> Sources,
	IReadOnlyList<DeviceView> Devices,
	IReadOnlyList<SensorView> Sensors,
	AutomationView Automation,
	IReadOnlyList<SettingView> Settings,
	IReadOnlyList<RaspberryInfoView> Raspberry,
	IReadOnlyList<ServiceView> Services,
	IReadOnlyList<PluginView> Plugins,
	DesktopActivity? Activity);

public sealed record DeviceView(string Device, string Name, string Icon, string Source, PowerState State,
	DateTimeOffset? OverrideUntil, bool Partial = false);

public sealed record SensorView(string Sensor, string Name, string Icon, string Source, string Value, string Unit, bool Available, DateTimeOffset? ObservedAt, double? Reading = null, double? Min = null, double? Max = null, ReadingKind Kind = ReadingKind.Number);

/// Automation, time context, sleep and the temporary suppressions are separate concepts
public sealed record AutomationView(
	bool Enabled,
	AutomationStatus Status,
	TimeContext TimeContext,
	bool SleepMode,
	DateTimeOffset? SleepModeUntil,
	bool SleepHold,
	DateTimeOffset? SleepHoldUntil,
	DateTimeOffset? SleepEntryAt,
	DateTimeOffset? HoldingUntil,
	bool DesktopHold,
	bool MotionActive,
	DateTimeOffset? LastMotionAt,
	bool WarningActive,
	bool SourcesOnline,
	bool CriticalSourcesOnline);

public sealed record SettingView(SettingKey Setting, string Value, string Unit);

public sealed record RaspberryInfoView(RaspberryInfo Info, string Value, string Unit);

public sealed record ServiceView(ServiceId Service, bool Running);


/// Everything the Kernel can say about why automation did or did not act
public sealed record AutomationDiagnostics(
	AutomationView Automation,
	string LastDecision,
	IReadOnlyList<SourceView> Sources,
	IReadOnlyList<DeviceView> Devices,
	IReadOnlyList<SensorView> Sensors,
	IReadOnlyList<SettingView> RelevantSettings,
	IReadOnlyList<DecisionRecord> RecentDecisions,
	IReadOnlyList<NotificationEntry> RecentEvents);

public sealed record DecisionRecord(DateTimeOffset At, string Subject, string Outcome, string Reason);
