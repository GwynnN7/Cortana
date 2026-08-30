using CortanaLib.Structures;
using StackExchange.Redis;

namespace CortanaLib;

public static class IpcHandler
{
	private static readonly Lazy<ConnectionMultiplexer?> Connection = new(CreateConnection, LazyThreadSafetyMode.ExecutionAndPublication);

	private static ConnectionMultiplexer? CreateConnection()
	{
		try
		{
			var options = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("CORTANA_REDIS") ?? "localhost");
			options.AbortOnConnectFail = false;
			options.ConnectRetry = 5;
			options.ConnectTimeout = 2000;
			options.SyncTimeout = 2000;
			return ConnectionMultiplexer.Connect(options);
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[IPC] Could not connect to Redis: {ex.Message}");
			return null;
		}
	}

	public static void Publish(EMessageCategory category, string message)
	{
		try
		{
			ISubscriber? publisher = Connection.Value?.GetSubscriber();
			publisher?.Publish(RedisChannel.Literal(category.ToString()), message, CommandFlags.FireAndForget);
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[IPC] Publish to {category} failed: {ex.Message}");
		}
	}

	public static void Subscribe(EMessageCategory category, Func<string, Task> onMessage)
	{
		try
		{
			ISubscriber? subscriber = Connection.Value?.GetSubscriber();
			subscriber?.Subscribe(RedisChannel.Literal(category.ToString())).OnMessage(async channelMessage =>
			{
				if (!channelMessage.Message.HasValue) return;
				try
				{
					await onMessage(channelMessage.Message.ToString());
				}
				catch (Exception ex)
				{
					DataHandler.Log($"[IPC] Handler for {category} threw: {ex.Message}");
				}
			});
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[IPC] Subscribe to {category} failed: {ex.Message}");
		}
	}

	public static async Task Shutdown()
	{
		if (!Connection.IsValueCreated || Connection.Value == null) return;
		try
		{
			await Connection.Value.CloseAsync();
			await Connection.Value.DisposeAsync();
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[IPC] Shutdown failed: {ex.Message}");
		}
	}
}
