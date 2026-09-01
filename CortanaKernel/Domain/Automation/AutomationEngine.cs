using System.Collections.Concurrent;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

/// What the engine needs to read about the world
public interface IAutomationWorld
{
	PowerState DeviceState(DeviceId device);
	bool ComputerConnected { get; }
	int? Light { get; }
	DateTimeOffset? LastMotionAt { get; }
	bool StationOnline { get; }
	bool AirQualityWarning { get; }
}

/// What the engine is allowed to do
public interface IAutomationEffects
{
	void SwitchDevice(DeviceId device, PowerState state, string reason);
	void TellComputer(string message);
	void Notify(NotificationSource source, string message, NotificationLevel level = NotificationLevel.Info);
	void Publish(IDomainEvent domainEvent);
}

/// Owns every runtime concept that decides whether Cortana acts
public sealed class AutomationEngine(SettingsStore settings, IAutomationWorld world, IAutomationEffects effects)
{
	private const int DecisionHistory = 40;

	private readonly Lock _gate = new();
	private readonly ConcurrentQueue<DecisionRecord> _decisions = new();

	private TimeContext _context = TimeContext.Day;
	private bool _started;

	private bool _sleepMode;
	private bool _sleepWasAutomatic;
	private DateTimeOffset? _sleepUntil;
	private DateTimeOffset? _sleepHoldUntil;
	private DateTimeOffset? _sleepEntryDueAt;

	private readonly Dictionary<DeviceId, DateTimeOffset> _overrides = new();

	private bool _lastMotionActive;
	private string _lastDecision = "not evaluated yet";

	public bool SleepMode
	{
		get { lock (_gate) return _sleepMode; }
	}

	public bool Enabled => settings.Flag(SettingKey.AutomationEnabled);

	public TimeContext Context
	{
		get { lock (_gate) return _context; }
	}

	public DateTimeOffset? OverrideUntil(DeviceId device)
	{
		lock (_gate) return _overrides.TryGetValue(device, out DateTimeOffset until) && until > DateTimeOffset.Now ? until : null;
	}

	public IReadOnlyList<DecisionRecord> Decisions => [.. _decisions.Reverse()];

	// ---------- lifecycle ----------

	/// Startup establishes the current context
	public void Start()
	{
		lock (_gate)
		{
			_context = AutomationRules.ContextAt(DateTimeOffset.Now, settings.Number(SettingKey.NightHour), settings.Number(SettingKey.MorningHour));
			_started = true;
		}

		Record("startup", "ready", $"time context is {Context}");
		Evaluate();
	}

	/// Called on a short cadence. Everything time-based expires here
	public void Tick()
	{
		if (!_started) return;

		DateTimeOffset now = DateTimeOffset.Now;

		TimeContext current = AutomationRules.ContextAt(now, settings.Number(SettingKey.NightHour), settings.Number(SettingKey.MorningHour));
		TimeContext previous;
		lock (_gate)
		{
			previous = _context;
			_context = current;
		}

		if (current != previous)
		{
			effects.Publish(new TimeContextChanged(current, now));
			if (current == TimeContext.Night) OnNightStarted();
			else OnMorningStarted();
		}

		bool reevaluate = false;

		if (Due(ref _sleepEntryDueAt, now))
		{
			Record("sleep", "activated", "the sleep entry delay elapsed");
			SetSleepMode(true, CommandOrigin.Automation, automatic: true);
			return;
		}

		if (Due(ref _sleepUntil, now))
		{
			Record("sleep", "expired", "the daytime sleep duration elapsed");
			SetSleepMode(false, CommandOrigin.Automation, automatic: true);
			return;
		}

		if (Due(ref _sleepHoldUntil, now))
		{
			Record("sleep hold", "expired", "reconsidering automatic sleep");
			ReconsiderAutomaticSleep();
			reevaluate = true;
		}

		List<DeviceId> expired;
		lock (_gate)
		{
			expired = [.. _overrides.Where(entry => entry.Value <= now).Select(entry => entry.Key)];
			foreach (DeviceId device in expired) _overrides.Remove(device);
		}

		foreach (DeviceId device in expired)
		{
			effects.Publish(new DeviceHoldChanged(device, null, now));
			effects.Notify(NotificationSource.Automation, "Automation resumed");
			reevaluate = true;
		}

		if (MotionActive(now) != _lastMotionActive) reevaluate = true;

		if (reevaluate) Evaluate();
	}

	// ---------- inputs ----------

	public void OnSensorReading()
	{
		Evaluate();
	}

	public void OnComputerConnected()
	{
		lock (_gate) _sleepEntryDueAt = null;

		if (SleepMode)
		{
			Record("sleep", "ended", "the computer powered on");
			SetSleepMode(false, CommandOrigin.Internal, automatic: true);
			return;
		}

		Evaluate();
	}

	public void OnComputerDisconnected()
	{
		DateTimeOffset now = DateTimeOffset.Now;

		if (!SleepMode && Context == TimeContext.Night && Enabled && !SleepHoldActive(now))
			StartSleepEntryDelay(now, "the computer went off during the night");

		Evaluate();
	}

	/// A user handling a device is both a possible wake signal and the start of a manual override
	public void OnUserDeviceAction(DeviceId device)
	{
		DateTimeOffset now = DateTimeOffset.Now;

		if (SleepMode && Context == TimeContext.Day)
		{
			Record("sleep", "ended", $"a user action on {device} after the morning boundary");
			SetSleepMode(false, CommandOrigin.User(CommandSurface.Internal), automatic: false);
		}

		if (!Enabled) return;

		TimeSpan duration = SleepMode
			? settings.Minutes(SettingKey.SleepManualOverrideMinutes)
			: settings.Minutes(SettingKey.ManualOverrideMinutes);

		bool started;
		lock (_gate)
		{
			started = !_overrides.TryGetValue(device, out DateTimeOffset until) || until <= now;
			if (started) _overrides[device] = now + duration;
		}

		if (!started) return;

		Record(device.ToString(), "manual override", $"held for {duration.TotalMinutes:0} minutes");
		effects.Publish(new DeviceHoldChanged(device, now + duration, now));
		effects.Notify(NotificationSource.Automation, $"Automation held {duration.TotalMinutes:0}m");
	}

	public void OnAutomationChanged(bool enabled, CommandOrigin origin)
	{
		ReleaseHolds($"automation turned {(enabled ? "on" : "off")}", evaluate: false);

		if (!enabled)
		{
			lock (_gate)
			{
				_sleepEntryDueAt = null;
				_sleepHoldUntil = null;
			}

			if (SleepMode) SetSleepMode(false, CommandOrigin.Internal, automatic: true);
			Record("automation", "disabled", $"requested by {origin}");
			return;
		}

		Record("automation", "enabled", $"requested by {origin}");

		if (SleepMode && origin.IsUser && Context == TimeContext.Day)
		{
			Record("sleep", "ended", "the user enabled automation after the morning boundary");
			SetSleepMode(false, origin, automatic: false);
			return;
		}

		Evaluate();
	}

	public void OnSettingsChanged(SettingKey key)
	{
		if (key is SettingKey.NotifyWeb or SettingKey.NotifyTelegram or SettingKey.NotifyDiscord) return;
		Evaluate();
	}

	// ---------- sleep ----------

	public Result<string> RequestSleepMode(SwitchAction action, CommandOrigin origin)
	{
		bool target = action switch
		{
			SwitchAction.On => true,
			SwitchAction.Off => false,
			_ => !SleepMode
		};

		if (target == SleepMode) return Result.Ok(target ? "Sleep mode is already active" : "Sleep mode is already off");

		SetSleepMode(target, origin, automatic: false);
		return Result.Ok(target ? "Sleep mode active" : "Sleep mode off");
	}

	private void SetSleepMode(bool active, CommandOrigin origin, bool automatic)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		bool wasAutomatic;
		List<DeviceId> cleared = [];

		lock (_gate)
		{
			wasAutomatic = _sleepWasAutomatic;
			_sleepMode = active;
			_sleepEntryDueAt = null;

			if (active)
			{
				_sleepWasAutomatic = automatic;
				// A daytime sleep is a nap with a fixed length while a nighttime one lasts until morning
				_sleepUntil = _context == TimeContext.Day ? now + settings.Minutes(SettingKey.DaySleepMinutes) : null;
				_sleepHoldUntil = null;
				cleared = [.. _overrides.Keys];
				_overrides.Clear();
			}
			else
			{
				_sleepUntil = null;
				// Turning an automatic nighttime sleep off is a request not to be put back to sleep straight away
				if (_context == TimeContext.Night && wasAutomatic && origin.IsUser)
					_sleepHoldUntil = now + settings.Minutes(SettingKey.SleepHoldMinutes);
			}
		}

		// Entering sleep cancels daytime holds, so those clients hear about it too
		foreach (DeviceId device in cleared) effects.Publish(new DeviceHoldChanged(device, null, now));

		string reason = active ? $"activated by {origin}" : $"ended by {origin}";
		effects.Publish(new SleepModeChanged(active, automatic, reason, now));
		effects.Notify(NotificationSource.Sleep, active ? "Sleep mode on" : "Sleep mode off");
		Record("sleep", active ? "active" : "off", reason);

		Evaluate();
	}

	private void OnNightStarted()
	{
		effects.Notify(NotificationSource.Automation, "Night started");

		if (SleepMode)
		{
			Record("sleep", "unchanged", "night started while sleep mode was already active");
			return;
		}

		if (SleepHoldActive(DateTimeOffset.Now))
		{
			Record("sleep", "suppressed", "a sleep hold is still running");
			return;
		}

		if (!Enabled)
		{
			Record("sleep", "suppressed", "automation is off");
			return;
		}

		if (world.ComputerConnected)
		{
			effects.TellComputer("It's late, you should go to sleep.");
			effects.Notify(NotificationSource.Automation, "Night, but the computer is on");
			Record("sleep", "deferred", "the computer is on, so the computer was notified instead");
			return;
		}

		StartSleepEntryDelay(DateTimeOffset.Now, "night started with the computer off");
	}

	private void OnMorningStarted()
	{
		lock (_gate)
		{
			_sleepHoldUntil = null;
			_sleepEntryDueAt = null;
		}

		effects.Notify(NotificationSource.Automation, "Good morning");

		if (SleepMode)
		{
			Record("sleep", "ended", "the morning boundary was reached");
			SetSleepMode(false, CommandOrigin.Automation, automatic: true);
			return;
		}

		Evaluate();
	}

	private void StartSleepEntryDelay(DateTimeOffset now, string reason)
	{
		TimeSpan delay = settings.Minutes(SettingKey.SleepEntryDelayMinutes);

		if (delay <= TimeSpan.Zero)
		{
			Record("sleep", "activated", reason);
			SetSleepMode(true, CommandOrigin.Automation, automatic: true);
			return;
		}

		lock (_gate) _sleepEntryDueAt = now + delay;
		Record("sleep", "pending", $"{reason}, entering in {delay.TotalMinutes:0} minutes");
	}

	private void ReconsiderAutomaticSleep()
	{
		if (SleepMode || Context != TimeContext.Night || !Enabled) return;

		if (world.ComputerConnected)
		{
			effects.TellComputer("It's late, you should go to sleep.");
			Record("sleep", "deferred", "the sleep hold expired but the computer is on");
			return;
		}

		Record("sleep", "activated", "the sleep hold expired and nighttime sleep still applies");
		SetSleepMode(true, CommandOrigin.Automation, automatic: true);
	}

	private bool SleepHoldActive(DateTimeOffset now)
	{
		lock (_gate) return _sleepHoldUntil.HasValue && _sleepHoldUntil.Value > now;
	}

	// ---------- evaluation ----------

	/// Reconciles the automatically controlled devices with the current conditions
	public void Evaluate()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		bool motion = MotionActive(now);
		_lastMotionActive = motion;

		bool overrideActive;
		lock (_gate) overrideActive = _overrides.TryGetValue(DeviceId.Lamp, out DateTimeOffset until) && until > now;

		var input = new LampInput(
			Enabled,
			overrideActive,
			SleepMode,
			motion,
			world.DeviceState(DeviceId.Lamp) == PowerState.On,
			world.Light,
			settings.Number(SettingKey.LightThreshold));

		LampDecision decision = AutomationRules.DecideLamp(input);
		_lastDecision = decision.Reason;

		if (decision.Target is not { } target || target == (input.LampIsOn ? PowerState.On : PowerState.Off)) return;

		Record(nameof(DeviceId.Lamp), target.ToString(), decision.Reason);
		effects.SwitchDevice(DeviceId.Lamp, target, decision.Reason);
	}

	private bool MotionActive(DateTimeOffset now) =>
		AutomationRules.MotionActive(world.LastMotionAt, now, MotionTimeout());

	private TimeSpan MotionTimeout() => AutomationRules.MotionTimeout(
		world.ComputerConnected,
		settings.Seconds(SettingKey.MotionTimeoutComputerOnSeconds),
		settings.Seconds(SettingKey.MotionTimeoutComputerOffSeconds));

	private bool Due(ref DateTimeOffset? deadline, DateTimeOffset now)
	{
		lock (_gate)
		{
			if (!deadline.HasValue || deadline.Value > now) return false;
			deadline = null;
			return true;
		}
	}

	private void Record(string subject, string outcome, string reason)
	{
		_decisions.Enqueue(new DecisionRecord(DateTimeOffset.Now, subject, outcome, reason));
		while (_decisions.Count > DecisionHistory) _decisions.TryDequeue(out _);
	}

	// ---------- projection ----------

	public string LastDecision => _lastDecision;

	public AutomationView View()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		bool enabled = Enabled;

		lock (_gate)
		{
			DateTimeOffset? holdingUntil = null;
			foreach (DateTimeOffset until in _overrides.Values)
				if (until > now && (holdingUntil is null || until > holdingUntil)) holdingUntil = until;

			AutomationStatus status = !enabled
				? AutomationStatus.Off
				: holdingUntil.HasValue ? AutomationStatus.Holding : AutomationStatus.Active;

			return new AutomationView(
				enabled,
				status,
				_context,
				_sleepMode,
				_sleepUntil,
				_sleepHoldUntil.HasValue && _sleepHoldUntil.Value > now,
				_sleepHoldUntil,
				_sleepEntryDueAt,
				holdingUntil,
				AutomationRules.MotionActive(world.LastMotionAt, now, MotionTimeout()),
				world.LastMotionAt,
				world.AirQualityWarning,
				world.StationOnline);
		}
	}

	/// Drops every active hold and hands control back to automation
	public void ReleaseHolds(string reason, bool evaluate = true)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		List<DeviceId> released;

		lock (_gate)
		{
			released = [.. _overrides.Where(entry => entry.Value > now).Select(entry => entry.Key)];
			_overrides.Clear();
		}

		foreach (DeviceId device in released) effects.Publish(new DeviceHoldChanged(device, null, now));

		if (released.Count > 0)
		{
			Record("automation", "resumed", reason);
			effects.Notify(NotificationSource.Automation, "Automation resumed");
		}

		if (evaluate) Evaluate();
	}
}
