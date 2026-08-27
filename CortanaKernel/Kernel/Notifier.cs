using System.Collections.Concurrent;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class Notifier
{
	private const int Capacity = 300;

	private static readonly ConcurrentQueue<LogEntry> Entries = new();

	public static void Send(ELogSource source, string message, ELogLevel level = ELogLevel.Info)
	{
		var entry = new LogEntry(DateTimeOffset.Now, level, source, message);

		Entries.Enqueue(entry);
		while (Entries.Count > Capacity) Entries.TryDequeue(out _);

		DataHandler.Log($"[{source}] {message}");

		if (HardwareApi.Sensors.LogDestinationEnabled(ESettings.LogToTelegram)) IpcHandler.Publish(EMessageCategory.Telegram, message);
		if (HardwareApi.Sensors.LogDestinationEnabled(ESettings.LogToDiscord)) IpcHandler.Publish(EMessageCategory.Discord, message);

		SystemEvents.Notify();
	}

	public static IReadOnlyList<LogEntry> Recent(int limit = Capacity) =>
		Entries.Reverse().Take(Math.Clamp(limit, 1, Capacity)).ToList();

	public static void Clear()
	{
		while (Entries.TryDequeue(out _)) { }
		SystemEvents.Notify();
	}
}
