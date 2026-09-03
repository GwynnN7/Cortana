using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Settings;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

public interface ISleepHost
{
	TimeContext Context { get; }
	bool AutomationEnabled { get; }
	void ClearHolds();
	void Reevaluate();
	void Record(string subject, string outcome, string reason);
}

public sealed class SleepEngine(SettingsStore settings, IAutomationWorld world, IAutomationEffects effects, ISleepHost host)
{
	private readonly Lock _gate = new();

	private bool _active;
	private bool _wasAutomatic;
	private DateTimeOffset? _until;
	private DateTimeOffset? _holdUntil;
	private DateTimeOffset? _entryDueAt;

	public bool Active
	{
		get { lock (_gate) return _active; }
	}

	public DateTimeOffset? Until
	{
		get { lock (_gate) return _until; }
	}

	public DateTimeOffset? HoldUntil
	{
		get { lock (_gate) return _holdUntil; }
	}

	public DateTimeOffset? EntryDueAt
	{
		get { lock (_gate) return _entryDueAt; }
	}

	public bool HoldActive(DateTimeOffset now)
	{
		lock (_gate) return _holdUntil.HasValue && _holdUntil.Value > now;
	}

	/// Returns true when something changed and the binds are worth re-running
	public bool Enabled => settings.Flag(SettingKey.SleepEnabled);

	public bool Tick(DateTimeOffset now)
	{
		if (!Enabled)
		{
			if (!Active) return false;

			Set(false, CommandOrigin.Internal, automatic: true);
			host.Record("sleep", "off", "the sleep service was switched off");
			return true;
		}

		if (Due(ref _entryDueAt, now))
		{
			host.Record("sleep", "activated", "the sleep entry delay elapsed");
			Set(true, CommandOrigin.Automation, automatic: true);
			return false;
		}

		if (Due(ref _until, now))
		{
			host.Record("sleep", "expired", "the daytime sleep duration elapsed");
			Set(false, CommandOrigin.Automation, automatic: true);
			return false;
		}

		if (!Due(ref _holdUntil, now)) return false;

		host.Record("sleep hold", "expired", "reconsidering automatic sleep");
		Reconsider();
		return true;
	}

	public Result<string> Request(SwitchAction action, CommandOrigin origin)
	{
		if (!Enabled) return Result.Fail<string>("Sleep is switched off");

		bool target = action switch
		{
			SwitchAction.On => true,
			SwitchAction.Off => false,
			_ => !Active
		};

		if (target == Active) return Result.Ok(target ? "Sleep mode is already active" : "Sleep mode is already off");

		Set(target, origin, automatic: false);
		return Result.Ok(target ? "Sleep mode active" : "Sleep mode off");
	}

	public void OnNightStarted()
	{
		if (!Enabled) return;

		effects.Notify(NotificationSource.Automation, "Night started",
			reason: $"the clock reached the configured night hour of {settings.Number(SettingKey.NightHour)}");

		if (Active)
		{
			host.Record("sleep", "unchanged", "night started while sleep mode was already active");
			return;
		}

		if (HoldActive(DateTimeOffset.Now))
		{
			host.Record("sleep", "suppressed", "a sleep hold is still running");
			return;
		}

		if (!host.AutomationEnabled)
		{
			host.Record("sleep", "suppressed", "automation is off");
			return;
		}

		if (world.ComputerConnected)
		{
			effects.TellComputer("It's late, you should go to sleep.");
			effects.Notify(NotificationSource.Automation, "Night, but the computer is on",
				reason: "automatic sleep is blocked while the computer is connected, so the computer was told instead");
			host.Record("sleep", "deferred", "the computer is on, so the computer was notified instead");
			return;
		}

		StartEntryDelay(DateTimeOffset.Now, "night started with the computer off");
	}

	public void OnMorningStarted()
	{
		lock (_gate)
		{
			_holdUntil = null;
			_entryDueAt = null;
		}

		effects.Notify(NotificationSource.Automation, "Good morning",
			reason: $"the clock reached the configured morning hour of {settings.Number(SettingKey.MorningHour)}");

		if (!Active)
		{
			host.Reevaluate();
			return;
		}

		host.Record("sleep", "ended", "the morning boundary was reached");
		Set(false, CommandOrigin.Automation, automatic: true);
	}

	/// Any deliberate action after the morning boundary means the night is over
	public void OnUserAction(string subject)
	{
		if (!Active || host.Context != TimeContext.Day) return;

		host.Record("sleep", "ended", $"a user action on {subject} after the morning boundary");
		Set(false, CommandOrigin.User(CommandSurface.Internal), automatic: false);
	}

	public void OnComputerConnected()
	{
		lock (_gate) _entryDueAt = null;

		if (!Active) return;

		host.Record("sleep", "ended", "the computer powered on");
		Set(false, CommandOrigin.Internal, automatic: true);
	}

	public void OnComputerDisconnected()
	{
		DateTimeOffset now = DateTimeOffset.Now;

		if (!Active && host.Context == TimeContext.Night && host.AutomationEnabled && !HoldActive(now))
			StartEntryDelay(now, "the computer went off during the night");
	}

	public void Suspend()
	{
		lock (_gate)
		{
			_entryDueAt = null;
			_holdUntil = null;
		}

		if (Active) Set(false, CommandOrigin.Internal, automatic: true);
	}

	public void Reconsider()
	{
		if (Active || host.Context != TimeContext.Night || !host.AutomationEnabled) return;

		if (world.ComputerConnected)
		{
			effects.TellComputer("It's late, you should go to sleep.");
			host.Record("sleep", "deferred", "the sleep hold expired but the computer is on");
			return;
		}

		host.Record("sleep", "activated", "the sleep hold expired and nighttime sleep still applies");
		Set(true, CommandOrigin.Automation, automatic: true);
	}

	private void Set(bool active, CommandOrigin origin, bool automatic)
	{
		DateTimeOffset now = DateTimeOffset.Now;
		bool wasAutomatic;

		lock (_gate)
		{
			wasAutomatic = _wasAutomatic;
			_active = active;
			_entryDueAt = null;

			if (active)
			{
				_wasAutomatic = automatic;
				// A daytime sleep is a nap with a fixed length while a nighttime one lasts until morning
				_until = host.Context == TimeContext.Day ? now + settings.Minutes(SettingKey.DaySleepMinutes) : null;
				_holdUntil = null;
			}
			else
			{
				_until = null;
				// Turning an automatic nighttime sleep off is a request not to be put back to sleep straight away
				if (host.Context == TimeContext.Night && wasAutomatic && origin.IsUser)
					_holdUntil = now + settings.Minutes(SettingKey.SleepHoldMinutes);
			}
		}

		if (active) host.ClearHolds();

		string reason = active ? $"activated by {origin}" : $"ended by {origin}";
		effects.Publish(new SleepModeChanged(active, automatic, reason, now));
		effects.Notify(NotificationSource.Sleep, active ? "Sleep mode on" : "Sleep mode off", reason: reason);
		host.Record("sleep", active ? "active" : "off", reason);

		host.Reevaluate();
	}

	private void StartEntryDelay(DateTimeOffset now, string reason)
	{
		TimeSpan delay = settings.Minutes(SettingKey.SleepEntryDelayMinutes);

		if (delay <= TimeSpan.Zero)
		{
			host.Record("sleep", "activated", reason);
			Set(true, CommandOrigin.Automation, automatic: true);
			return;
		}

		lock (_gate) _entryDueAt = now + delay;
		host.Record("sleep", "pending", $"{reason}, entering in {delay.TotalMinutes:0} minutes");
	}

	private bool Due(ref DateTimeOffset? deadline, DateTimeOffset now)
	{
		lock (_gate)
		{
			if (!deadline.HasValue || deadline.Value > now) return false;
			deadline = null;
			return true;
		}
	}
}
