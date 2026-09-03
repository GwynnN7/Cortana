namespace CortanaLib.Contracts;

public static class Wire
{
	public const string Magic = "cortana";
	public const int Version = 1;

	public const string Hello = "hello";
	public const string Reading = "reading";
	public const string Facts = "facts";
	public const string Ping = "ping";
	public const string Reply = "reply";
	public const string Activity = "activity";
	public const string Command = "command";
	public const string Welcome = "welcome";
}

public sealed record SourceHello(
	string Type,
	string Magic,
	int Version,
	string Source,
	SourceKind Kind,
	IReadOnlyList<string> Outputs,
	IReadOnlyList<string> Inputs,
	IReadOnlyDictionary<string, string>? Facts = null);

/// How a source updates what it says about itself, without reconnecting
public sealed record SourceDescription(
	string Type,
	IReadOnlyDictionary<string, string> Values);

public sealed record SourceReading(
	string Type,
	IReadOnlyDictionary<string, double> Values);

public sealed record SourceWelcome(
	string Type,
	bool Accepted,
	string Detail);

public sealed record SourceCommand(
	string Type,
	string Id,
	string Device,
	string State);
