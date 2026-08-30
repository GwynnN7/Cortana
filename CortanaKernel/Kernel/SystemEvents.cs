using System.Collections.Concurrent;
using System.Threading.Channels;

namespace CortanaKernel.Kernel;

internal static class SystemEvents
{
	private static readonly ConcurrentDictionary<Guid, Channel<byte>> Subscribers = new();

		public static Subscription Subscribe()
	{
		var id = Guid.NewGuid();
		Channel<byte> channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
		Subscribers[id] = channel;
		return new Subscription(id, channel.Reader);
	}

	public static void Notify()
	{
		foreach (Channel<byte> channel in Subscribers.Values) channel.Writer.TryWrite(1);
		Task.Run(() => PushService.RefreshStatus());
	}

	internal sealed class Subscription(Guid id, ChannelReader<byte> reader) : IDisposable
	{
		public ChannelReader<byte> Reader { get; } = reader;

		public void Dispose()
		{
			if (Subscribers.TryRemove(id, out Channel<byte>? channel)) channel.Writer.TryComplete();
		}
	}
}
