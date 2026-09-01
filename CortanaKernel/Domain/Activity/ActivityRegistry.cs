using CortanaLib.Contracts;

namespace CortanaKernel.Domain.Activity;

public sealed class ActivityRegistry
{
	private readonly Lock _gate = new();
	private DesktopActivity? _current;

	public DesktopActivity? Current
	{
		get { lock (_gate) return _current; }
	}

	public bool Set(DesktopActivity activity)
	{
		lock (_gate)
		{
			bool changed = _current is null
				|| _current.Category != activity.Category
				|| _current.Subject != activity.Subject
				|| _current.Fullscreen != activity.Fullscreen
				|| _current.Locked != activity.Locked;

			_current = activity;
			return changed;
		}
	}

	public void Clear()
	{
		lock (_gate) _current = null;
	}
}

public static class ActivityRules
{
	public static bool DoNotDisturb(DesktopActivity? activity) =>
		activity is { Fullscreen: true, Category: ActivityCategory.Gaming or ActivityCategory.Media };
}
