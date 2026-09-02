using CortanaLib.Contracts;

namespace CortanaKernel.Domain.Volition;

public interface IVolitionRepository
{
	VolitionState Load();
	void Save(VolitionState state);
}

public sealed class VolitionStore(IVolitionRepository repository)
{
	private readonly Lock _gate = new();
	private VolitionState _state = repository.Load();

	public VolitionState State
	{
		get { lock (_gate) return _state; }
	}

	public void Update(Func<VolitionState, VolitionState> change)
	{
		lock (_gate)
		{
			_state = change(_state);
			repository.Save(_state);
		}
	}
}

public static class VolitionRules
{
	public static readonly TimeSpan GreetingWindow = TimeSpan.FromHours(3);

	public static bool Quiet(VolitionState state, DateTimeOffset now) =>
		state.QuietUntil is { } until && until > now;

	public static bool ShouldGreet(VolitionState state, DateTimeOffset now, int morningHour, bool sleepMode)
	{
		if (sleepMode || Quiet(state, now)) return false;

		DateOnly today = DateOnly.FromDateTime(now.LocalDateTime);
		if (state.LastGreeted == today) return false;

		var opens = new DateTimeOffset(today.Year, today.Month, today.Day, morningHour, 0, 0, now.Offset);

		return now >= opens && now < opens + GreetingWindow;
	}
}
