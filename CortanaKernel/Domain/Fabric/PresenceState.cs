namespace CortanaKernel.Domain.Fabric;

public sealed class PresenceState
{
	private readonly Lock _gate = new();

	public DateTimeOffset? LastMotionAt
	{
		get { lock (_gate) return field; }
		set { lock (_gate) field = value; }
	}
}
