
namespace CortanaLib.Contracts;

public enum ScheduleTrigger
{
	Once,
	Interval,
	Daily,
	Weekly,
	Event
}

/// Facts a schedule can hook onto
public enum ScheduleEvent
{
	ComputerTurnedOn,
	ComputerTurnedOff,
	NightStarted,
	MorningStarted,
	SleepModeStarted,
	SleepModeEnded,
	MotionDetected,
	WarningRaised
}

public enum ScheduleActionType
{
	SwitchDevice,
	CommandComputer,
	CommandRaspberry,
	ChangeSetting,
	SendNotification,
	ControlService,
	SetSleepMode,
	SetAutomation
}

public sealed record ScheduleAction(ScheduleActionType Type, string Target = "", string Value = "");

public sealed record Schedule
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required ScheduleTrigger Trigger { get; init; }
	public required ScheduleAction Action { get; init; }

	public DateTimeOffset? At { get; init; }
	public int IntervalSeconds { get; init; }
	public int Hour { get; init; }
	public int Minute { get; init; }
	public DayOfWeek? Day { get; init; }
	public ScheduleEvent? Event { get; init; }
	public bool RunOnce { get; init; }
	public int MinimumIntervalSeconds { get; init; }

	public bool Enabled { get; init; } = true;
	public string Owner { get; init; } = "";
	public DateTimeOffset CreatedAt { get; init; }
	public DateTimeOffset? LastRun { get; init; }
	public string? LastResult { get; init; }
}

public sealed record CreateScheduleRequest(
	string Name,
	ScheduleTrigger Trigger,
	ScheduleActionType ActionType,
	string Target = "",
	string Value = "",
	DateTimeOffset? At = null,
	int IntervalSeconds = 0,
	int Hour = 0,
	int Minute = 0,
	DayOfWeek? Day = null,
	ScheduleEvent? Event = null,
	bool RunOnce = false,
	int MinimumIntervalSeconds = 0,
	string Owner = "");

public sealed record ScheduleCommandRequest(string Command);

public sealed record ScheduleView(Schedule Schedule, DateTimeOffset? NextRun);

public sealed record ScheduleListResponse(IReadOnlyList<ScheduleView> Schedules);
