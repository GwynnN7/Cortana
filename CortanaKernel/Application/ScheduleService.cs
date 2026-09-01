using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Scheduling;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed class ScheduleService(
	IScheduleRepository repository,
	DeviceService devices,
	AutomationService automation,
	SettingsService settings,
	ServiceControlService services,
	NotificationService notifications,
	IEventBus bus) : BackgroundService
{
	private static readonly TimeSpan MissedGrace = TimeSpan.FromHours(1);
	private static readonly TimeSpan MaxSleep = TimeSpan.FromMinutes(5);

	private readonly Lock _gate = new();
	private readonly SemaphoreSlim _wakeup = new(0);
	private Dictionary<string, Schedule> _schedules = new();

	public IReadOnlyList<Schedule> All()
	{
		lock (_gate) return [.. _schedules.Values.OrderBy(schedule => ScheduleTiming.NextRun(schedule, DateTimeOffset.Now) ?? DateTimeOffset.MaxValue)];
	}

	public IReadOnlyList<ScheduleView> Views() =>
		[.. All().Select(schedule => new ScheduleView(schedule, ScheduleTiming.NextRun(schedule, DateTimeOffset.Now)))];

	public Schedule? Get(string id)
	{
		lock (_gate) return _schedules.GetValueOrDefault(id);
	}

	public Result<Schedule> Create(CreateScheduleRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Name)) return Result.Fail<Schedule>("A schedule needs a name");

		if (request.Trigger == ScheduleTrigger.Once && request.At == null) return Result.Fail<Schedule>("A Once trigger needs a moment in time");
		if (request.Trigger == ScheduleTrigger.Interval && request.IntervalSeconds < 10) return Result.Fail<Schedule>("An Interval trigger needs at least 10 seconds");
		if (request.Trigger == ScheduleTrigger.Weekly && request.Day == null) return Result.Fail<Schedule>("A Weekly trigger needs a day");
		if (request.Trigger == ScheduleTrigger.Event && request.Event == null) return Result.Fail<Schedule>("An Event trigger needs an event");

		Result<ScheduleAction> action = BuildAction(request.ActionType, request.Target, request.Value);
		if (!action.IsOk) return Result.Fail<Schedule>(action.Error);

		var schedule = new Schedule
		{
			Id = Guid.NewGuid().ToString("N")[..12],
			Name = request.Name.Trim(),
			Trigger = request.Trigger,
			Action = action.Value,
			At = request.At,
			IntervalSeconds = request.IntervalSeconds,
			Hour = Math.Clamp(request.Hour, 0, 23),
			Minute = Math.Clamp(request.Minute, 0, 59),
			Day = request.Day,
			Event = request.Event,
			RunOnce = request.RunOnce,
			MinimumIntervalSeconds = Math.Max(0, request.MinimumIntervalSeconds),
			Owner = request.Owner,
			CreatedAt = DateTimeOffset.Now
		};

		lock (_gate)
		{
			_schedules[schedule.Id] = schedule;
			Persist();
		}

		Wake();
		return Result.Ok(schedule);
	}

	public bool Delete(string id)
	{
		lock (_gate)
		{
			if (!_schedules.Remove(id)) return false;
			Persist();
		}

		Wake();
		return true;
	}

	public Schedule? SetEnabled(string id, bool enabled)
	{
		lock (_gate)
		{
			if (!_schedules.TryGetValue(id, out Schedule? schedule)) return null;

			Schedule updated = schedule with { Enabled = enabled };
			_schedules[id] = updated;
			Persist();
			Wake();
			return updated;
		}
	}

	public async Task<Result<string>> RunNow(string id)
	{
		Schedule? schedule = Get(id);
		return schedule == null ? Result.Fail<string>($"Schedule '{id}' not found") : await Execute(schedule);
	}

	private static Result<ScheduleAction> BuildAction(ScheduleActionType type, string target, string value)
	{
		switch (type)
		{
			case ScheduleActionType.SwitchDevice:
				if (!Enum.TryParse(target, true, out DeviceId _)) return Fail($"Unknown device '{target}'");
				if (!Enum.TryParse(value, true, out SwitchAction _)) return Fail($"Unknown action '{value}'");
				break;

			case ScheduleActionType.SwitchRoom:
			case ScheduleActionType.SetSleepMode:
			case ScheduleActionType.SetAutomation:
				if (!Enum.TryParse(value, true, out SwitchAction _)) return Fail($"Unknown action '{value}'");
				break;

			case ScheduleActionType.CommandComputer:
				if (!Enum.TryParse(target, true, out ComputerCommand _)) return Fail($"Unknown computer command '{target}'");
				break;

			case ScheduleActionType.CommandRaspberry:
				if (!Enum.TryParse(target, true, out RaspberryCommand _)) return Fail($"Unknown raspberry command '{target}'");
				break;

			case ScheduleActionType.ChangeSetting:
				if (!Enum.TryParse(target, true, out SettingKey _)) return Fail($"Unknown setting '{target}'");
				if (string.IsNullOrWhiteSpace(value)) return Fail("A setting change needs a value");
				break;

			case ScheduleActionType.SendNotification:
				if (string.IsNullOrWhiteSpace(value)) return Fail("A notification needs a message");
				break;

			case ScheduleActionType.ControlService:
				if (!Enum.TryParse(target, true, out ServiceId _)) return Fail($"Unknown service '{target}'");
				if (!Enum.TryParse(value, true, out ServiceAction _)) return Fail($"Unknown service action '{value}'");
				break;

			default:
				return Fail($"Unsupported action '{type}'");
		}

		return Result.Ok(new ScheduleAction(type, target, value));
	}

	private static Result<ScheduleAction> Fail(string message) => Result.Fail<ScheduleAction>(message);

	private static string Why(Schedule schedule) => schedule.Trigger switch
	{
		ScheduleTrigger.Event => $"the {schedule.Event} event fired",
		ScheduleTrigger.Once => $"it was due at {schedule.At:dd MMM HH:mm}",
		ScheduleTrigger.Interval => $"its {schedule.IntervalSeconds}s interval elapsed",
		ScheduleTrigger.Daily => $"its daily {schedule.Hour:00}:{schedule.Minute:00} time was reached",
		ScheduleTrigger.Weekly => $"its {schedule.Day} {schedule.Hour:00}:{schedule.Minute:00} time was reached",
		_ => "it was run"
	};

	private async Task<Result<string>> Dispatch(ScheduleAction action, CancellationToken token)
	{
		CommandOrigin origin = CommandOrigin.Scheduler;

		switch (action.Type)
		{
			case ScheduleActionType.SwitchDevice:
				return devices.Switch(Enum.Parse<DeviceId>(action.Target, true), Enum.Parse<SwitchAction>(action.Value, true), origin);

			case ScheduleActionType.SwitchRoom:
				return devices.SwitchRoom(Enum.Parse<SwitchAction>(action.Value, true), origin);

			case ScheduleActionType.CommandComputer:
				return await devices.CommandComputer(Enum.Parse<ComputerCommand>(action.Target, true), action.Value, origin, token);

			case ScheduleActionType.CommandRaspberry:
				return await devices.CommandRaspberry(Enum.Parse<RaspberryCommand>(action.Target, true), action.Value, token);

			case ScheduleActionType.ChangeSetting:
				return settings.Write(Enum.Parse<SettingKey>(action.Target, true), action.Value);

			case ScheduleActionType.SendNotification:
				NotificationChannel? channel = Enum.TryParse(action.Target, true, out NotificationChannel parsed) ? parsed : null;
				return notifications.Send(new NotifyRequest(action.Value, NotificationSource.Schedule, NotificationLevel.Info, channel));

			case ScheduleActionType.ControlService:
				return await services.Control(Enum.Parse<ServiceId>(action.Target, true), Enum.Parse<ServiceAction>(action.Value, true), token);

			case ScheduleActionType.SetSleepMode:
				return automation.SetSleepMode(Enum.Parse<SwitchAction>(action.Value, true), origin);

			case ScheduleActionType.SetAutomation:
				return automation.SetAutomation(Enum.Parse<SwitchAction>(action.Value, true), origin);

			default:
				return Result.Fail<string>($"Unsupported action '{action.Type}'");
		}
	}

	private async Task<Result<string>> Execute(Schedule schedule, CancellationToken token = default)
	{
		Result<string> result;
		try
		{
			result = await Dispatch(schedule.Action, token);
		}
		catch (Exception ex)
		{
			result = Result.Fail<string>(ex.Message);
		}

		string outcome = result.Match(value => value, error => $"Failed: {error}");
		notifications.Raise(NotificationSource.Schedule, $"{schedule.Name}: {outcome}",
			result.IsOk ? NotificationLevel.Info : NotificationLevel.Warning,
			Why(schedule));

		lock (_gate)
		{
			if (_schedules.TryGetValue(schedule.Id, out Schedule? current))
			{
				_schedules[schedule.Id] = current with
				{
					LastRun = DateTimeOffset.Now,
					LastResult = outcome,
					Enabled = current.Enabled && !(current.RunOnce && current.Trigger == ScheduleTrigger.Event)
				};
				Persist();
			}
		}

		bus.Publish(new ScheduleTriggered(schedule.Id, schedule.Name, outcome, DateTimeOffset.Now));
		return result;
	}

	// ---------- the loop ----------

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		lock (_gate) _schedules = repository.Load().ToDictionary(schedule => schedule.Id);

		SubscribeToEvents();
		await RunMissed(stoppingToken);

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await _wakeup.WaitAsync(TimeUntilNext(), stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			int fired = await FireDue(stoppingToken);
			if (fired == 0) await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}

	private void SubscribeToEvents()
	{
		bus.Subscribe<ComputerConnectionChanged>(fact =>
			Raise(fact.Connected ? ScheduleEvent.ComputerTurnedOn : ScheduleEvent.ComputerTurnedOff));

		bus.Subscribe<TimeContextChanged>(fact =>
			Raise(fact.Context == TimeContext.Night ? ScheduleEvent.NightStarted : ScheduleEvent.MorningStarted));

		bus.Subscribe<SleepModeChanged>(fact =>
			Raise(fact.Active ? ScheduleEvent.SleepModeStarted : ScheduleEvent.SleepModeEnded));

		bus.Subscribe<MotionDetected>(_ => Raise(ScheduleEvent.MotionDetected));

		bus.Subscribe<AirQualityWarningChanged>(fact =>
		{
			if (fact.Warning) Raise(ScheduleEvent.AirQualityWarning);
		});
	}

	/// Event schedules fire for every matching fact, unless they are one-shot or still inside their minimum interval
	private void Raise(ScheduleEvent trigger)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		List<Schedule> due;

		lock (_gate)
		{
			due = [.. _schedules.Values.Where(schedule => ScheduleTiming.ShouldFireOnEvent(schedule, trigger, now))];
			if (due.Count == 0) return;

			due = Claim(due);
			Persist();
		}

		_ = Task.Run(async () =>
		{
			foreach (Schedule schedule in due) await Execute(schedule);
		});
	}

	/// A Once schedule that came due while the Kernel was down runs if it is still recent, and is otherwise recorded as missed
	private async Task RunMissed(CancellationToken token)
	{
		List<Schedule> missed;
		lock (_gate)
		{
			DateTimeOffset cutoff = DateTimeOffset.Now - MissedGrace;

			missed =
			[
				.. _schedules.Values.Where(schedule =>
					schedule.Enabled && schedule.Trigger == ScheduleTrigger.Once && schedule.LastRun == null &&
					schedule.At != null && schedule.At <= DateTimeOffset.Now)
			];

			foreach (Schedule stale in missed.Where(schedule => schedule.At < cutoff).ToList())
			{
				Log.Write("Schedule", $"Dropping '{stale.Name}', it was due at {stale.At} which is outside the grace window");
				_schedules[stale.Id] = stale with { LastRun = DateTimeOffset.Now, LastResult = "Missed while offline" };
				missed.Remove(stale);
			}

			missed = Claim(missed);
			Persist();
		}

		foreach (Schedule schedule in missed) await Execute(schedule, token);
	}

	private List<Schedule> Claim(List<Schedule> due)
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

	private TimeSpan TimeUntilNext()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		DateTimeOffset? soonest = null;

		lock (_gate)
			foreach (Schedule schedule in _schedules.Values)
			{
				DateTimeOffset? next = ScheduleTiming.NextRun(schedule, now);
				if (next != null && (soonest == null || next < soonest)) soonest = next;
			}

		if (soonest == null) return MaxSleep;

		TimeSpan delta = soonest.Value - now;
		if (delta <= TimeSpan.Zero) return TimeSpan.Zero;
		return delta < MaxSleep ? delta : MaxSleep;
	}

	private async Task<int> FireDue(CancellationToken token)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		List<Schedule> due;

		lock (_gate)
		{
			due =
			[
				.. _schedules.Values
					.Where(schedule => schedule.Enabled && schedule.Trigger != ScheduleTrigger.Event)
					.Where(schedule => ScheduleTiming.NextRun(schedule, now) is { } next && next <= now)
			];

			if (due.Count > 0)
			{
				due = Claim(due);
				Persist();
			}
		}

		foreach (Schedule schedule in due) await Execute(schedule, token);
		return due.Count;
	}

	private void Wake()
	{
		try { _wakeup.Release(); } catch (SemaphoreFullException) { /* already awake */ }
	}

	private void Persist() => repository.Save([.. _schedules.Values]);
}
