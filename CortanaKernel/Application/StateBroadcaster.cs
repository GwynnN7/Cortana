using System.Collections.Concurrent;
using System.Threading.Channels;
using CortanaKernel.Domain.Common;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

/// External processes cannot touch the in-memory bus, so every fact becomes "there is a newer snapshot" and clients pull a fresh one
public sealed class StateBroadcaster
{
	private readonly ConcurrentDictionary<Guid, Channel<byte>> _state = new();
	private readonly ConcurrentDictionary<Guid, (NotificationChannel Channel, Channel<NotificationEnvelope> Sink)> _notifications = new();

	public StateBroadcaster(IEventBus bus) => bus.SubscribeAll(_ => Touch());

	public void Push(NotificationChannel channel, NotificationEntry entry)
	{
		foreach ((NotificationChannel target, Channel<NotificationEnvelope> sink) in _notifications.Values)
			if (target == channel)
				sink.Writer.TryWrite(new NotificationEnvelope(channel, entry));
	}

	public void Touch()
	{
		foreach (Channel<byte> channel in _state.Values) channel.Writer.TryWrite(1);
	}

	public StateSubscription SubscribeState()
	{
		var id = Guid.NewGuid();
		Channel<byte> channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
		_state[id] = channel;
		return new StateSubscription(channel.Reader, () => _state.TryRemove(id, out _));
	}

	public NotificationSubscription SubscribeNotifications(NotificationChannel target)
	{
		var id = Guid.NewGuid();
		Channel<NotificationEnvelope> channel = Channel.CreateBounded<NotificationEnvelope>(
			new BoundedChannelOptions(64) { FullMode = BoundedChannelFullMode.DropOldest });
		_notifications[id] = (target, channel);
		return new NotificationSubscription(channel.Reader, () => _notifications.TryRemove(id, out _));
	}

	public sealed class StateSubscription(ChannelReader<byte> reader, Action dispose) : IDisposable
	{
		public ChannelReader<byte> Reader { get; } = reader;
		public void Dispose() => dispose();
	}

	public sealed class NotificationSubscription(ChannelReader<NotificationEnvelope> reader, Action dispose) : IDisposable
	{
		public ChannelReader<NotificationEnvelope> Reader { get; } = reader;
		public void Dispose() => dispose();
	}
}
