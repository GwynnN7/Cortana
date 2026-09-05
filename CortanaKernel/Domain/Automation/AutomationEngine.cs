using System.Collections.Concurrent;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

/// What the engine needs to read about the world
public interface IAutomationWorld
{
	PowerState DeviceState(string device);
	bool ComputerConnected { get; }
	DateTimeOffset? LastMotionAt { get; }
	bool SourcesOnline { get; }
	bool CriticalSourcesOnline { get; }
	bool WarningActive { get; }
	bool DesktopBusy { get; }
	bool Reported { get; }
	bool Sustained { get; }
	Fabric.Reading? Read(string sensor);
	string DeviceName(string device);
	IReadOnlyList<Bind> Binds { get; }
}

/// What the engine is allowed to do
public interface IAutomationEffects
{
	void SwitchDevice(string device, PowerState state, string reason);
	void Observe(string sensor, double value);
	void TellComputer(string message);
	void Notify(NotificationSource source, string message, NotificationLevel level = NotificationLevel.Info, string? reason = null);
	void Publish(IDomainEvent domainEvent);
}

/// Owns every runtime concept that decides whether Cortana acts
public sealed class AutomationEngine(SettingsStore settings, DayNightClock clock, IAutomationWorld world, IAutomationEffects effects) : ISleepHost
{
	private SleepEngine sleep => field ??= new SleepEngine(settings, world, effects, this);

	public SleepEngine Sleep => sleep;

	public bool AutomationEnabled => Enabled;

	public void ClearHolds()
	{
		List<string> cleared;
		lock (_gate)
		{
			cleared = [.. _overrides.Keys];
			_overrides.Clear();
		}

		foreach (string device in cleared) effects.Publish(new DeviceHoldChanged(device, null, DateTimeOffset.Now));
	}

	public void Reevaluate() => Evaluate();

	public Result<string> RequestSleepMode(SwitchAction action, CommandOrigin origin) => sleep.Request(action, origin);

	private const int DecisionHistory = 40;

	private readonly Lock _gate = new();
	private readonly Lock _evaluateGate = new();
	private readonly ConcurrentQueue<DecisionRecord> _decisions = new();

	private bool _started;


	private readonly Dictionary<string, DateTimeOffset> _overrides = new();

	private bool _lastMotionActive;
	private string _lastDecision = "not evaluated yet";
	private readonly ConcurrentDictionary<string, BindStatusView> _bindStatus = new(StringComparer.OrdinalIgnoreCase);

	public bool SleepMode => sleep.Active;

	public bool Enabled => settings.Flag(SettingKey.AutomationEnabled);

	public TimeContext Context => clock.Context;

	public DateTimeOffset? OverrideUntil(string device)
	{
		lock (_gate) return _overrides.TryGetValue(device, out DateTimeOffset until) && until > DateTimeOffset.Now ? until : null;
	}

	public IReadOnlyList<DecisionRecord> Decisions => [.. _decisions.Reverse()];

	// ---------- lifecycle ----------

	/// Startup establishes the current context
	public void Start()
	{
		clock.Establish(DateTimeOffset.Now);
		lock (_gate) _started = true;

		Record("startup", "ready", $"time context is {Context}");
		Evaluate();
	}

	/// Called on a short cadence. Everything time-based expires here
	public void Tick()
	{
		if (!_started) return;

		DateTimeOffset now = DateTimeOffset.Now;

		if (clock.Advance(now) is { } current)
		{
			effects.Publish(new TimeContextChanged(current, now));
			if (current == TimeContext.Night) sleep.OnNightStarted();
			else sleep.OnMorningStarted();
		}

		bool reevaluate = sleep.Tick(now);

		List<string> expired;
		lock (_gate)
		{
			expired = [.. _overrides.Where(entry => entry.Value <= now).Select(entry => entry.Key)];
			foreach (string device in expired) _overrides.Remove(device);
		}

		foreach (string device in expired)
		{
			effects.Publish(new DeviceHoldChanged(device, null, now));
			effects.Notify(NotificationSource.Automation, "Automation resumed", reason: $"the hold on {device} expired");
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
		sleep.OnComputerConnected();
		Evaluate();
	}

	public void OnComputerDisconnected()
	{
		sleep.OnComputerDisconnected();
		Evaluate();
	}

	/// A user handling a device is both a possible wake signal and the start of a manual override
	public void OnUserDeviceAction(string device)
	{
		DateTimeOffset now = DateTimeOffset.Now;

		sleep.OnUserAction(device);

		if (!Enabled || !Managed.Contains(device)) return;

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
		effects.Notify(NotificationSource.Automation, $"Automation held {duration.TotalMinutes:0}m", reason: $"a user action on {device} while automation was in control");
	}

	public void OnAutomationChanged(bool enabled, CommandOrigin origin)
	{
		ReleaseHolds($"automation turned {(enabled ? "on" : "off")}", evaluate: false);

		if (!enabled)
		{
			sleep.Suspend();
			Record("automation", "disabled", $"requested by {origin}");
			return;
		}

		Record("automation", "enabled", $"requested by {origin}");

		if (SleepMode && origin.IsUser && Context == TimeContext.Day)
		{
			sleep.Request(SwitchAction.Off, origin);
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

	// ---------- evaluation ----------

	/// Readings, the tick and the computer all reach this from their own threads, and a device must
	/// only be switched once, so the whole pass is serialised
	public void Evaluate()
	{
		lock (_evaluateGate) Pass();
	}

	private void Pass()
	{
		DateTimeOffset now = DateTimeOffset.Now;
		bool present = MotionActive(now);
		_lastMotionActive = present;

		effects.Observe(SensorIds.Presence, present ? 1 : 0);
		effects.Observe(SensorIds.Night, Context == TimeContext.Night ? 1 : 0);
		effects.Observe(SensorIds.Sleep, SleepMode ? 1 : 0);

		string[] live = [.. world.Binds.Select(bind => bind.Id)];
		foreach (string stale in _bindStatus.Keys.Where(id => !live.Contains(id, StringComparer.OrdinalIgnoreCase)))
			_bindStatus.TryRemove(stale, out _);

		if (!Enabled)
		{
			_lastDecision = "automation is off";
			foreach (Bind bind in world.Binds) Status(bind, "off", "automation is off");
			return;
		}

		foreach (Bind bind in world.Binds)
		{
			bool held;
			lock (_gate) held = _overrides.TryGetValue(bind.Device, out DateTimeOffset until) && until > now;

			if (held)
			{
				_lastDecision = $"{bind.Device} is under a manual hold";
				Status(bind, "held", $"{world.DeviceName(bind.Device)} is under a manual hold");
				continue;
			}

			if (world.DesktopBusy)
			{
				_lastDecision = "the computer is busy with a game or something fullscreen";
				Status(bind, "held", "the computer is busy with a game or something fullscreen");
				continue;
			}

			bool isOn = world.DeviceState(bind.Device) == PowerState.On;

			BindDecision decision = BindRules.Decide(bind, isOn, world.Read);

			_lastDecision = decision.Reason;
			Status(bind, decision.Suspended ? "suspended" : decision.Target?.ToString().ToLowerInvariant() ?? "waiting",
				decision.Reason, decision.Suspended);

			if (decision.Target is not { } target || target == (isOn ? PowerState.On : PowerState.Off)) continue;

			string name = world.DeviceName(bind.Device);
			Record(bind.Device, target.ToString(), decision.Reason);
			effects.SwitchDevice(bind.Device, target, decision.Reason);
			effects.Notify(NotificationSource.Automation, $"{name} {target.ToString().ToLowerInvariant()}", reason: decision.Reason);
		}
	}

	private void Status(Bind bind, string outcome, string reason, bool suspended = false) =>
		_bindStatus[bind.Id] = new BindStatusView(bind.Id, suspended, outcome, reason);

	public IReadOnlyList<BindStatusView> BindStatus => [.. _bindStatus.Values];

	private string[] Managed => [.. world.Binds.Where(bind => bind.HoldsOnManualAction).Select(bind => bind.Device)];

	private bool MotionActive(DateTimeOffset now) => AutomationRules.Present(
		world.LastMotionAt, now, settings.Seconds(SettingKey.MotionTimeoutSeconds),
		world.Reported, world.Sustained, _lastMotionActive);

	private bool Due(ref DateTimeOffset? deadline, DateTimeOffset now)
	{
		lock (_gate)
		{
			if (!deadline.HasValue || deadline.Value > now) return false;
			deadline = null;
			return true;
		}
	}

	public void Record(string subject, string outcome, string reason)
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
				: holdingUntil.HasValue || world.DesktopBusy ? AutomationStatus.Holding
				: world.Binds.Any(bind => bind.Enabled) ? AutomationStatus.Active : AutomationStatus.Idle;

			return new AutomationView(
				enabled,
				status,
				Context,
				sleep.Active,
				sleep.Until,
				sleep.HoldActive(now),
				sleep.HoldUntil,
				sleep.EntryDueAt,
				holdingUntil,
				world.DesktopBusy,
				MotionActive(now),
				world.LastMotionAt,
				world.WarningActive,
				world.SourcesOnline,
				world.CriticalSourcesOnline);
		}
	}

	/// Drops every active hold and hands control back to automation
	public void ReleaseHolds(string reason, bool evaluate = true)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		List<string> released;

		lock (_gate)
		{
			released = [.. _overrides.Where(entry => entry.Value > now).Select(entry => entry.Key)];
			_overrides.Clear();
		}

		foreach (string device in released) effects.Publish(new DeviceHoldChanged(device, null, now));

		if (released.Count > 0)
		{
			Record("automation", "resumed", reason);
			effects.Notify(NotificationSource.Automation, "Automation resumed", reason: reason);
		}

		if (evaluate) Evaluate();
	}
}
