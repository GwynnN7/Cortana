using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class ScheduleService
{
	private static readonly string FilePath = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Schedules.json");
	private static readonly TimeSpan MissedGrace = TimeSpan.FromHours(1);
	private static readonly TimeSpan MaxSleep = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan MinCadence = TimeSpan.FromSeconds(1);

	private static readonly Lock StoreLock = new();
	private static readonly SemaphoreSlim Wakeup = new(0);
	private static readonly CancellationTokenSource Lifetime = new();

	private static Dictionary<string, Schedule> _schedules = new();
	private static Task? _loop;

	public static void Start()
	{
		lock (StoreLock)
		{
			List<Schedule> loaded = DataHandler.DeserializeJson<List<Schedule>>(FilePath) ?? [];
			_schedules = loaded.ToDictionary(s => s.Id);
		}

		RunMissed();
		_loop = Task.Run(Loop);
	}

	public static void Interrupt()
	{
		try { Lifetime.Cancel(); } catch (ObjectDisposedException) { }
	}

	public static IReadOnlyList<Schedule> All()
	{
		lock (StoreLock) return _schedules.Values.OrderBy(s => NextRun(s) ?? DateTimeOffset.MaxValue).ToList();
	}

	public static Schedule? Get(string id)
	{
		lock (StoreLock) return _schedules.GetValueOrDefault(id);
	}

	public static Result<Schedule, string> Create(PostSchedule request)
	{
		if (string.IsNullOrWhiteSpace(request.Name)) return Result<Schedule, string>.Failure("Name is required");
		if (!Enum.TryParse(request.Trigger, true, out EScheduleTrigger trigger)) return Result<Schedule, string>.Failure($"Unknown trigger '{request.Trigger}'. Valid: {string.Join(", ", Enum.GetNames<EScheduleTrigger>())}");
		if (!Enum.TryParse(request.ActionType, true, out EScheduleAction actionType)) return Result<Schedule, string>.Failure($"Unknown action '{request.ActionType}'. Valid: {string.Join(", ", Enum.GetNames<EScheduleAction>())}");

		EScheduleEvent? scheduleEvent = null;
		if (trigger == EScheduleTrigger.Event)
		{
			if (!Enum.TryParse(request.Event, true, out EScheduleEvent parsed)) return Result<Schedule, string>.Failure($"Unknown event '{request.Event}'. Valid: {string.Join(", ", Enum.GetNames<EScheduleEvent>())}");
			scheduleEvent = parsed;
		}

		if (trigger == EScheduleTrigger.Interval && request.IntervalSeconds < 10) return Result<Schedule, string>.Failure("IntervalSeconds must be at least 10");
		if (trigger == EScheduleTrigger.Once && request.At == null) return Result<Schedule, string>.Failure("At is required for a Once trigger");
		if (trigger == EScheduleTrigger.Weekly && request.Day == null) return Result<Schedule, string>.Failure("Day is required for a Weekly trigger");

		Result<ScheduleAction, string> action = BuildAction(actionType, request.Target, request.Value);
		if (!action.IsOk) return Result<Schedule, string>.Failure(action.Match(_ => "", error => error));

		var schedule = new Schedule
		{
			Id = Guid.NewGuid().ToString("N")[..12],
			Name = request.Name.Trim(),
			Trigger = trigger,
			Action = action.Match(value => value, _ => null!),
			At = request.At,
			IntervalSeconds = request.IntervalSeconds,
			Hour = Math.Clamp(request.Hour, 0, 23),
			Minute = Math.Clamp(request.Minute, 0, 59),
			Day = request.Day,
			Event = scheduleEvent,
			Owner = request.Owner,
			CreatedAt = DateTimeOffset.Now
		};

		lock (StoreLock)
		{
			_schedules[schedule.Id] = schedule;
			Persist();
		}

		Wake();
		return Result<Schedule, string>.Success(schedule);
	}

	private static Result<ScheduleAction, string> BuildAction(EScheduleAction type, string target, string value)
	{
		switch (type)
		{
			case EScheduleAction.Device:
				if (!Enum.TryParse(target, true, out EDevice _)) return Fail<ScheduleAction>($"Unknown device '{target}'");
				if (!Enum.TryParse(value, true, out ESwitchAction _)) return Fail<ScheduleAction>($"Unknown switch action '{value}'");
				break;
			case EScheduleAction.Room:
				if (!Enum.TryParse(value, true, out ESwitchAction _)) return Fail<ScheduleAction>($"Unknown switch action '{value}'");
				break;
			case EScheduleAction.Computer:
				if (!Enum.TryParse(target, true, out EComputerCommand _)) return Fail<ScheduleAction>($"Unknown computer command '{target}'");
				break;
			case EScheduleAction.Raspberry:
				if (!Enum.TryParse(target, true, out ERaspberryCommand _)) return Fail<ScheduleAction>($"Unknown raspberry command '{target}'");
				break;
			case EScheduleAction.Setting:
				if (!Enum.TryParse(target, true, out ESettings _)) return Fail<ScheduleAction>($"Unknown setting '{target}'");
				if (!int.TryParse(value, out int _)) return Fail<ScheduleAction>("Setting value must be a number");
				break;
			case EScheduleAction.Notify:
				if (!Enum.TryParse(target, true, out EMessageCategory _)) return Fail<ScheduleAction>($"Unknown category '{target}'");
				if (string.IsNullOrWhiteSpace(value)) return Fail<ScheduleAction>("Notify needs a message");
				break;
			case EScheduleAction.Subfunction:
				if (!Enum.TryParse(target, true, out ESubFunctionType _)) return Fail<ScheduleAction>($"Unknown subfunction '{target}'");
				if (!Enum.TryParse(value, true, out ESubfunctionAction _)) return Fail<ScheduleAction>($"Unknown subfunction action '{value}'");
				break;
			default:
				return Fail<ScheduleAction>("Unsupported action");
		}

		return Result<ScheduleAction, string>.Success(new ScheduleAction(type, target, value));
	}

	private static Result<T, string> Fail<T>(string message) => Result<T, string>.Failure(message);

	public static bool Delete(string id)
	{
		lock (StoreLock)
		{
			if (!_schedules.Remove(id)) return false;
			Persist();
		}

		Wake();
		return true;
	}

	public static Schedule? SetEnabled(string id, bool enabled)
	{
		lock (StoreLock)
		{
			if (!_schedules.TryGetValue(id, out Schedule? schedule)) return null;
			Schedule updated = schedule with { Enabled = enabled };
			_schedules[id] = updated;
			Persist();
			Wake();
			return updated;
		}
	}

	public static async Task<StringResult> RunNow(string id)
	{
		Schedule? schedule = Get(id);
		if (schedule == null) return StringResult.Failure("Schedule not found");

		return await Execute(schedule);
	}

	public static void RaiseEvent(EScheduleEvent trigger)
	{
		List<Schedule> due;
		lock (StoreLock)
		{
			due = _schedules.Values.Where(s => s.Enabled && s.Trigger == EScheduleTrigger.Event && s.Event == trigger).ToList();
		}

		if (due.Count == 0) return;
		_ = Task.Run(async () =>
		{
			foreach (Schedule schedule in due) await Execute(schedule);
		});
	}

	public static DateTimeOffset? NextRun(Schedule schedule)
	{
		if (!schedule.Enabled) return null;

		DateTimeOffset now = DateTimeOffset.Now;
		switch (schedule.Trigger)
		{
			case EScheduleTrigger.Once:
				return schedule.LastRun != null ? null : schedule.At;

			case EScheduleTrigger.Interval:
				DateTimeOffset anchor = schedule.LastRun ?? schedule.CreatedAt;
				DateTimeOffset next = anchor.AddSeconds(schedule.IntervalSeconds);
				if (next >= now) return next;

				long skipped = (long)((now - anchor).TotalSeconds / schedule.IntervalSeconds);
				return anchor.AddSeconds(skipped * schedule.IntervalSeconds);

			case EScheduleTrigger.Daily:
				return NextAt(now, schedule.Hour, schedule.Minute, null);

			case EScheduleTrigger.Weekly:
				return NextAt(now, schedule.Hour, schedule.Minute, schedule.Day);

			default:
				return null;
		}
	}

	private static DateTimeOffset NextAt(DateTimeOffset now, int hour, int minute, DayOfWeek? day)
	{
		DateTimeOffset candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, minute, 0, now.Offset);
		if (candidate <= now) candidate = candidate.AddDays(1);

		if (day == null) return candidate;

		while (candidate.DayOfWeek != day.Value) candidate = candidate.AddDays(1);
		return candidate;
	}

	private static void RunMissed()
	{
		List<Schedule> missed;
		lock (StoreLock)
		{
			DateTimeOffset cutoff = DateTimeOffset.Now.Subtract(MissedGrace);
			missed = _schedules.Values
				.Where(s => s.Enabled && s.Trigger == EScheduleTrigger.Once && s.LastRun == null && s.At != null && s.At <= DateTimeOffset.Now)
				.ToList();

			foreach (Schedule stale in missed.Where(s => s.At < cutoff).ToList())
			{
				DataHandler.Log($"[Schedule] Dropping '{stale.Name}', due {stale.At} which is outside the grace window");
				_schedules[stale.Id] = stale with { LastRun = DateTimeOffset.Now, LastResult = "Missed while offline" };
				missed.Remove(stale);
			}

			missed = Claim(missed);
			Persist();
		}

		if (missed.Count == 0) return;
		_ = Task.Run(async () =>
		{
			foreach (Schedule schedule in missed) await Execute(schedule);
		});
	}

	private static List<Schedule> Claim(List<Schedule> due)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		var claimed = new List<Schedule>(due.Count);

		foreach (Schedule schedule in due)
		{
			if (!_schedules.TryGetValue(schedule.Id, out Schedule? current)) continue;
			Schedule updated = current with { LastRun = now };
			_schedules[schedule.Id] = updated;
			claimed.Add(updated);
		}

		return claimed;
	}

	private static async Task Loop()
	{
		while (!Lifetime.IsCancellationRequested)
		{
			TimeSpan wait = TimeUntilNext();

			try
			{
				await Wakeup.WaitAsync(wait, Lifetime.Token);
			}
			catch (OperationCanceledException)
			{
				return;
			}
			catch (Exception ex)
			{
				DataHandler.Log($"[Schedule] Loop wait failed: {ex.Message}");
				return;
			}

			int fired = await FireDue();

			if (fired == 0) await Task.Delay(MinCadence, Lifetime.Token);
		}
	}

	private static TimeSpan TimeUntilNext()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset? soonest = null;

		lock (StoreLock)
		{
			foreach (Schedule schedule in _schedules.Values)
			{
				DateTimeOffset? next = NextRun(schedule);
				if (next == null) continue;
				if (soonest == null || next < soonest) soonest = next;
			}
		}

		if (soonest == null) return MaxSleep;

		TimeSpan delta = soonest.Value - now;
		if (delta <= TimeSpan.Zero) return TimeSpan.Zero;
		return delta < MaxSleep ? delta : MaxSleep;
	}

	private static async Task<int> FireDue()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		List<Schedule> due;

		lock (StoreLock)
		{
			due = _schedules.Values
				.Where(s => s.Enabled && s.Trigger != EScheduleTrigger.Event)
				.Where(s => NextRun(s) is { } next && next <= now)
				.ToList();

			if (due.Count > 0)
			{
				due = Claim(due);
				Persist();
			}
		}

		foreach (Schedule schedule in due) await Execute(schedule);
		return due.Count;
	}

	private static async Task<StringResult> Execute(Schedule schedule)
	{
		StringResult result;
		try
		{
			result = await Dispatch(schedule.Action);
		}
		catch (Exception ex)
		{
			result = StringResult.Failure(ex.Message);
		}

		string outcome = result.Match(value => value, error => $"Failed: {error}");
		DataHandler.Log($"[Schedule] '{schedule.Name}' -> {outcome}");

		lock (StoreLock)
		{
			if (_schedules.TryGetValue(schedule.Id, out Schedule? current))
			{
				_schedules[schedule.Id] = current with { LastRun = DateTimeOffset.Now, LastResult = outcome };
				Persist();
			}
		}

		SystemEvents.Notify();
		return result;
	}

	private static async Task<StringResult> Dispatch(ScheduleAction action)
	{
		switch (action.Type)
		{
			case EScheduleAction.Device:
				return HardwareApi.Devices.Switch(Enum.Parse<EDevice>(action.Target, true), Enum.Parse<ESwitchAction>(action.Value, true));

			case EScheduleAction.Room:
				return HardwareApi.Devices.SwitchRoom(Enum.Parse<ESwitchAction>(action.Value, true));

			case EScheduleAction.Computer:
				return await HardwareApi.Devices.CommandComputer(Enum.Parse<EComputerCommand>(action.Target, true), string.IsNullOrEmpty(action.Value) ? null : action.Value);

			case EScheduleAction.Raspberry:
				ERaspberryCommand command = Enum.Parse<ERaspberryCommand>(action.Target, true);
				return command == ERaspberryCommand.Command
					? await HardwareApi.Raspberry.RunCommand(action.Value)
					: HardwareApi.Raspberry.Command(command);

			case EScheduleAction.Setting:
				return HardwareApi.Sensors.SetSettings(Enum.Parse<ESettings>(action.Target, true), int.Parse(action.Value));

			case EScheduleAction.Notify:
				IpcHandler.Publish(Enum.Parse<EMessageCategory>(action.Target, true), action.Value);
				return StringResult.Success("Notified");

			case EScheduleAction.Subfunction:
				return await Bootloader.SubfunctionCall(Enum.Parse<ESubFunctionType>(action.Target, true), Enum.Parse<ESubfunctionAction>(action.Value, true));

			default:
				return StringResult.Failure("Unsupported action");
		}
	}

	private static void Wake()
	{
		try { Wakeup.Release(); } catch (SemaphoreFullException) { }
	}

	private static void Persist()
	{
		try
		{
			_schedules.Values.ToList().Serialize().Dump(FilePath);
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[Schedule] Could not save: {ex.Message}");
		}
	}
}
