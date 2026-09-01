using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Common;

public interface IDomainEvent
{
	DateTimeOffset At { get; }
}

public sealed record DeviceStateChanged(DeviceId Device, PowerState State, CommandOrigin Origin, DateTimeOffset At) : IDomainEvent;

public sealed record MotionDetected(DateTimeOffset At) : IDomainEvent;

public sealed record SensorReadingReceived(DateTimeOffset At) : IDomainEvent;

public sealed record SensorAvailabilityChanged(bool Online, DateTimeOffset At) : IDomainEvent;

public sealed record ComputerConnectionChanged(bool Connected, DateTimeOffset At) : IDomainEvent;

public sealed record SleepModeChanged(bool Active, bool Automatic, string Reason, DateTimeOffset At) : IDomainEvent;

public sealed record AutomationEnabledChanged(bool Enabled, CommandOrigin Origin, DateTimeOffset At) : IDomainEvent;

public sealed record TimeContextChanged(TimeContext Context, DateTimeOffset At) : IDomainEvent;

public sealed record AirQualityWarningChanged(bool Warning, DateTimeOffset At) : IDomainEvent;

public sealed record DeviceHoldChanged(DeviceId Device, DateTimeOffset? Until, DateTimeOffset At) : IDomainEvent;

public sealed record DesktopActivityChanged(DesktopActivity Activity, DateTimeOffset At) : IDomainEvent;

public sealed record SettingChanged(SettingKey Setting, string Value, DateTimeOffset At) : IDomainEvent;

public sealed record ScheduleTriggered(string Id, string Name, string Outcome, DateTimeOffset At) : IDomainEvent;

public sealed record ServiceStateChanged(ServiceId Service, bool Running, DateTimeOffset At) : IDomainEvent;

public sealed record NotificationRaised(NotificationEntry Entry, DateTimeOffset At) : IDomainEvent;

public sealed record ConversationUpdated(string Conversation, DateTimeOffset At) : IDomainEvent;
