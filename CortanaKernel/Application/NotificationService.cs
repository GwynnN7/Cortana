using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Notifications;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed class NotificationService(
	NotificationLog log,
	IEnumerable<INotificationSink> sinks,
	SettingsStore settings,
	IEventBus bus)
{
	private readonly IReadOnlyList<INotificationSink> _sinks = [.. sinks];

	public void Raise(NotificationSource source, string message, NotificationLevel level = NotificationLevel.Info)
	{
		var entry = new NotificationEntry(DateTimeOffset.Now, source, level, message);

		log.Add(entry);
		Log.Write(source.ToString(), message);
		bus.Publish(new NotificationRaised(entry, entry.Timestamp));

		foreach (INotificationSink sink in _sinks.Where(sink => Wants(sink.Channel)))
			_ = Deliver(sink, entry);
	}

	/// Explicit send, used by schedules and by the API, targeting one channel or all of them
	public Result<string> Send(NotifyRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.Message)) return Result.Fail<string>("Nothing to send");

		var entry = new NotificationEntry(DateTimeOffset.Now, request.Source, request.Level, request.Message);
		log.Add(entry);
		bus.Publish(new NotificationRaised(entry, entry.Timestamp));

		IEnumerable<INotificationSink> targets = request.Channel is { } channel
			? _sinks.Where(sink => sink.Channel == channel)
			: _sinks.Where(sink => Wants(sink.Channel));

		foreach (INotificationSink sink in targets) _ = Deliver(sink, entry);

		return Result.Ok("Sent");
	}

	public IReadOnlyList<NotificationEntry> Recent(int limit) => log.Recent(limit);

	public void Clear() => log.Clear();

	private bool Wants(NotificationChannel channel) => channel switch
	{
		NotificationChannel.Web => settings.Flag(SettingKey.NotifyWeb),
		NotificationChannel.Telegram => settings.Flag(SettingKey.NotifyTelegram),
		NotificationChannel.Discord => settings.Flag(SettingKey.NotifyDiscord),
		_ => false
	};

	private static async Task Deliver(INotificationSink sink, NotificationEntry entry)
	{
		try
		{
			await sink.Deliver(entry);
		}
		catch (Exception ex)
		{
			Log.Error("Notifications", $"{sink.Channel} delivery failed: {ex.Message}");
		}
	}
}
