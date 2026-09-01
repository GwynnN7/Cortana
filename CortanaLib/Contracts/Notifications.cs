using CortanaLib.Primitives;

namespace CortanaLib.Contracts;

public sealed record NotificationEntry(
	DateTimeOffset Timestamp,
	NotificationSource Source,
	NotificationLevel Level,
	string Message,
	string? Reason = null);

public sealed record NotificationEnvelope(NotificationChannel Channel, NotificationEntry Notification);

public sealed record NotificationListResponse(IReadOnlyList<NotificationEntry> Entries);
