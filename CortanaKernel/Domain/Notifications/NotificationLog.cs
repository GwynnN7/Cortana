using System.Collections.Concurrent;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Notifications;

/// Bounded in-memory history of everything Cortana told the user
public sealed class NotificationLog
{
	private const int Capacity = 300;

	private readonly ConcurrentQueue<NotificationEntry> _entries = new();

	public void Add(NotificationEntry entry)
	{
		_entries.Enqueue(entry);
		while (_entries.Count > Capacity) _entries.TryDequeue(out _);
	}

	public IReadOnlyList<NotificationEntry> Recent(int limit = Capacity) =>
		[.. _entries.Reverse().Take(Math.Clamp(limit, 1, Capacity))];

	public void Clear()
	{
		while (_entries.TryDequeue(out _)) { }
	}
}

/// A delivery channel
public interface INotificationSink
{
	NotificationChannel Channel { get; }
	Task Deliver(NotificationEntry entry, CancellationToken token = default);
}
