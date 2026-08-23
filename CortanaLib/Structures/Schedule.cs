namespace CortanaLib.Structures;

public enum EScheduleTrigger
{
	Once,
	Interval,
	Daily,
	Weekly,
	Event
}

public enum EScheduleEvent
{
	ComputerOn,
	ComputerOff,
	NightStart,
	MorningStart
}

public enum EScheduleAction
{
	Device,
	Room,
	Computer,
	Raspberry,
	Setting,
	Notify,
	Subfunction
}

public record ScheduleAction(EScheduleAction Type, string Target = "", string Value = "");

public record Schedule
{
	public required string Id { get; init; }
	public required string Name { get; init; }
	public required EScheduleTrigger Trigger { get; init; }
	public required ScheduleAction Action { get; init; }

	public DateTimeOffset? At { get; init; }
	public int IntervalSeconds { get; init; }
	public int Hour { get; init; }
	public int Minute { get; init; }
	public DayOfWeek? Day { get; init; }
	public EScheduleEvent? Event { get; init; }

	public bool Enabled { get; init; } = true;
	public string Owner { get; init; } = "";
	public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
	public DateTimeOffset? LastRun { get; init; }
	public string? LastResult { get; init; }
}

public record PostSchedule(
	string Name,
	string Trigger,
	string ActionType,
	string Target = "",
	string Value = "",
	DateTimeOffset? At = null,
	int IntervalSeconds = 0,
	int Hour = 0,
	int Minute = 0,
	DayOfWeek? Day = null,
	string? Event = null,
	string Owner = "");

public record PostScheduleUpdate(string Command);

public record ScheduleResponse(Schedule Schedule, DateTimeOffset? NextRun) : IApiResponse;
public record ScheduleListResponse(IReadOnlyList<ScheduleResponse> Schedules) : IApiResponse;
