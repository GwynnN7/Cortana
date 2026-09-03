using CortanaKernel.Domain.Settings;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Automation;

/// Day and night are produced here, so nothing else has to know the hours
public sealed class DayNightClock(SettingsStore settings)
{
	private readonly Lock _gate = new();
	private TimeContext _context = TimeContext.Day;

	public TimeContext Context
	{
		get { lock (_gate) return _context; }
	}

	public TimeContext Establish(DateTimeOffset now)
	{
		lock (_gate)
		{
			_context = At(now);
			return _context;
		}
	}

	/// The new context, but only when it just changed
	public TimeContext? Advance(DateTimeOffset now)
	{
		lock (_gate)
		{
			TimeContext current = At(now);
			if (current == _context) return null;

			_context = current;
			return current;
		}
	}

	private TimeContext At(DateTimeOffset now) =>
		AutomationRules.ContextAt(now, settings.Number(SettingKey.NightHour), settings.Number(SettingKey.MorningHour));
}
