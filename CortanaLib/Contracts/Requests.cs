using CortanaLib.Primitives;

namespace CortanaLib.Contracts;

public sealed record SwitchRequest(SwitchAction Action = SwitchAction.Toggle);

public sealed record SettingRequest(string Value);

public sealed record ComputerRequest(ComputerCommand Command, string Argument = "");

public sealed record RaspberryRequest(RaspberryCommand Command, string Argument = "");

public sealed record ServiceRequest(ServiceAction Action);

public sealed record NotifyRequest(string Message, NotificationSource Source = NotificationSource.Kernel, NotificationLevel Level = NotificationLevel.Info, NotificationChannel? Channel = null);
