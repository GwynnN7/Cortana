using System.Collections.Concurrent;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Common;

/// In-process typed publish/subscribe
public interface IEventBus
{
	void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent;
	IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent;
	IDisposable SubscribeAll(Action<IDomainEvent> handler);
}

public sealed class EventBus : IEventBus
{
	private readonly ConcurrentDictionary<Type, List<Action<IDomainEvent>>> _handlers = new();
	private readonly List<Action<IDomainEvent>> _all = [];
	private readonly Lock _gate = new();

	public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
	{
		Type type = domainEvent.GetType();
		Action<IDomainEvent>[] typed;
		Action<IDomainEvent>[] all;

		lock (_gate)
		{
			typed = _handlers.TryGetValue(type, out List<Action<IDomainEvent>>? list) ? [.. list] : [];
			all = [.. _all];
		}

		foreach (Action<IDomainEvent> handler in typed) Invoke(() => handler(domainEvent), type.Name);
		foreach (Action<IDomainEvent> handler in all) Invoke(() => handler(domainEvent), type.Name);
	}

	public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
	{
		Action<IDomainEvent> dispatch = fact => handler((TEvent)fact);
		lock (_gate) _handlers.GetOrAdd(typeof(TEvent), _ => []).Add(dispatch);
		return new Subscription(() =>
		{
			lock (_gate)
			{
				if (_handlers.TryGetValue(typeof(TEvent), out List<Action<IDomainEvent>>? list)) list.Remove(dispatch);
			}
		});
	}

	public IDisposable SubscribeAll(Action<IDomainEvent> handler)
	{
		lock (_gate) _all.Add(handler);
		return new Subscription(() => { lock (_gate) _all.Remove(handler); });
	}

	private static void Invoke(Action action, string name)
	{
		try
		{
			action();
		}
		catch (Exception ex)
		{
			Log.Error("EventBus", $"A handler for {name} threw: {ex.Message}");
		}
	}

	private sealed class Subscription(Action dispose) : IDisposable
	{
		private bool _disposed;

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			dispose();
		}
	}
}
