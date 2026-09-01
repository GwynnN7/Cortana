using CortanaLib.Primitives;

namespace CortanaLib.Contracts;

/// Coherent read model of everything Cortana currently believes
public sealed record CortanaSnapshot(
	DateTimeOffset Timestamp,
	Mood Mood,
	string MoodReason,
	IReadOnlyList<DeviceView> Devices,
	IReadOnlyList<SensorView> Sensors,
	AutomationView Automation,
	IReadOnlyList<SettingView> Settings,
	IReadOnlyList<RaspberryInfoView> Raspberry,
	IReadOnlyList<ServiceView> Services,
	MetricsView? ComputerMetrics,
	MetricsView RaspberryMetrics,
	DesktopActivity? Activity);

public sealed record DeviceView(DeviceId Device, PowerState State, DateTimeOffset? OverrideUntil);

public sealed record SensorView(SensorId Sensor, string Value, string Unit, bool Available, DateTimeOffset? ObservedAt);

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
	bool AirQualityWarning,
	bool StationOnline);

public sealed record SettingView(SettingKey Setting, string Value, string Unit);

public sealed record RaspberryInfoView(RaspberryInfo Info, string Value, string Unit);

public sealed record ServiceView(ServiceId Service, bool Running);

public sealed record MetricsView(
	string Host,
	string Os,
	double CpuLoad,
	double CpuTemp,
	double MemoryUsed,
	double MemoryTotal,
	double GpuLoad,
	double GpuTemp,
	double DiskUsed,
	double DiskTotal,
	long Uptime,
	DateTimeOffset Timestamp,
	bool Stale);

/// Everything the Kernel can say about why automation did or did not act
public sealed record AutomationDiagnostics(
	AutomationView Automation,
	string LastDecision,
	IReadOnlyList<DeviceView> Devices,
	IReadOnlyList<SensorView> Sensors,
	IReadOnlyList<SettingView> RelevantSettings,
	IReadOnlyList<DecisionRecord> RecentDecisions,
	IReadOnlyList<NotificationEntry> RecentEvents);

public sealed record DecisionRecord(DateTimeOffset At, string Subject, string Outcome, string Reason);
