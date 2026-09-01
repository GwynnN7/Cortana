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
	private readonly ConcurrentDictionary<Type, List<Delegate>> _handlers = new();
	private readonly List<Action<IDomainEvent>> _all = [];
	private readonly Lock _gate = new();

	public void Publish<TEvent>(TEvent domainEvent) where TEvent : IDomainEvent
	{
		Delegate[] typed;
		Action<IDomainEvent>[] all;

		lock (_gate)
		{
			typed = _handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list) ? [.. list] : [];
			all = [.. _all];
		}

		foreach (Delegate handler in typed) Invoke(() => ((Action<TEvent>)handler)(domainEvent), typeof(TEvent).Name);
		foreach (Action<IDomainEvent> handler in all) Invoke(() => handler(domainEvent), typeof(TEvent).Name);
	}

	public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IDomainEvent
	{
		lock (_gate) _handlers.GetOrAdd(typeof(TEvent), _ => []).Add(handler);
		return new Subscription(() =>
		{
			lock (_gate)
			{
				if (_handlers.TryGetValue(typeof(TEvent), out List<Delegate>? list)) list.Remove(handler);
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
