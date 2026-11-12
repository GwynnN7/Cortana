using CortanaLib.Primitives;

namespace CortanaLib.Contracts;

public enum SourceKind
{
	Station,
	Board,
	Host,
	Computer
}

public enum SourceState
{
	Offline,
	Online
}

public enum ReadingKind
{
	Boolean,
	Number
}

public enum TriggerKind
{
	IsTrue,
	IsFalse,
	Below,
	Above,
	Outside,
	Changed
}

public enum BindMode
{
	Any,
	All
}

public sealed record SourceDescriptor(
	string Id,
	SourceKind Kind,
	IReadOnlyList<string> Outputs,
	IReadOnlyList<string> Inputs);

public sealed record ChannelRef(string Source, string Channel);

public sealed record VirtualDevice(
	string Id,
	string Name,
	IReadOnlyList<ChannelRef> Channels,
	string IconOn = "\U0001F7E2",
	string IconOff = "",
	bool Pulse = false,
	string? PoweredBy = null,
	bool InStatus = false);

public sealed record VirtualSensor(
	string Id,
	string Name,
	string Source,
	string Channel,
	string Unit = "",
	ReadingKind Kind = ReadingKind.Number,
	string IconHigh = "\U0001F535",
	string IconLow = "",
	double? Min = null,
	double? Max = null,
	double Offset = 0,
	bool InStatus = false);

/// A reading Cortana works out from other readings — the one kind of sensor no hardware can announce.
/// Presence is one of these and nothing more: motion and the desk, OR'd, held for a while
public sealed record Composite(
	string Id,
	string Name,
	IReadOnlyList<CompositeInput> Inputs,
	BindMode Mode = BindMode.Any,
	int? HoldSeconds = null,
	string IconHigh = "\U0001F6B6",
	string IconLow = "\U0001FAE5",
	bool InStatus = false);

/// `Starts` may switch the composite on; an input without it can only keep one that is already on.
/// That is the whole reason a desktop woken from the other side of the country lights nothing: the
/// desk holds a presence somebody else established, and never announces one of its own
public sealed record CompositeInput(
	string Sensor,
	TriggerKind Kind = TriggerKind.IsTrue,
	double? Low = null,
	double? High = null,
	bool Starts = true);

public sealed record Registrations(
	IReadOnlyList<VirtualDevice> Devices,
	IReadOnlyList<VirtualSensor> Sensors,
	IReadOnlyList<Composite> Composites);

public sealed record ChannelView(
	string Source,
	string Channel,
	bool IsOutput,
	bool Registered,
	string? RegisteredAs);

public sealed record ChannelListResponse(IReadOnlyList<ChannelView> Channels);

/// What a source says about itself: not a reading, but the slow facts that describe the machine.
/// Ordered, because the source decides how it wants to be read
public sealed record SourceFact(string Key, string Value);

public sealed record SourceView(
	string Id,
	string Name,
	SourceKind Kind,
	SourceState State,
	DateTimeOffset? LastSeen,
	int Outputs,
	int Inputs,
	IReadOnlyList<SourceFact> Facts);

public sealed record Trigger(
	string Sensor,
	TriggerKind Kind,
	double? Low = null,
	double? High = null,
	bool Sustains = true);

public sealed record Bind(
	string Id,
	string Device,
	IReadOnlyList<Trigger> Triggers,
	BindMode Mode = BindMode.All,
	bool Enabled = true,
	bool HoldsOnManualAction = true,
	int? ReleaseAfterSeconds = null,
	string Name = "");

/// Warnings take the same triggers as binds: a sustaining one has to hold for the warning to fire,
/// the rest are what raise it
public sealed record Warning(
	string Id,
	string Name,
	string Message,
	IReadOnlyList<Trigger> Triggers,
	NotificationLevel Level = NotificationLevel.Alert,
	int CooldownMinutes = 45,
	bool Enabled = true,
	string Icon = "\u26A0\uFE0F",
	bool InStatus = true);

public sealed record WarningView(
	Warning Warning,
	bool Active,
	DateTimeOffset? Since);

public sealed record WarningListResponse(IReadOnlyList<WarningView> Warnings, IReadOnlyList<string> Adrift);

public sealed record PluginView(
	string Id,
	string Name,
	string Purpose,
	bool Active,
	bool CanDisable,
	string Detail);

public sealed record PluginListResponse(IReadOnlyList<PluginView> Plugins);

public sealed record DashboardLayout(
	IReadOnlyList<string> Sensors,
	IReadOnlyList<string> Devices);

public sealed record BindStatusView(string Bind, bool Suspended, string Outcome, string Reason);

public sealed record BindListResponse(IReadOnlyList<Bind> Binds, IReadOnlyList<BindStatusView> Status, IReadOnlyList<string> Adrift);

public sealed record SourceListResponse(IReadOnlyList<SourceView> Sources);
