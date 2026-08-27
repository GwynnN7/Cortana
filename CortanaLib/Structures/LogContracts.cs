namespace CortanaLib.Structures;

public enum ELogLevel
{
	Info,
	Warning,
	Alert
}

public enum ELogSource
{
	Kernel,
	Motion,
	AirQuality,
	Devices,
	Computer,
	Sensors,
	Automation,
	Schedule,
	Subfunction
}

public record LogEntry(DateTimeOffset Timestamp, ELogLevel Level, ELogSource Source, string Message) : IApiResponse;
public record LogListResponse(IReadOnlyList<LogEntry> Entries) : IApiResponse;
