namespace CortanaLib.Contracts;

public sealed record HistoryPoint(DateTimeOffset At, double Value);

public sealed record HistorySeries(
	string Metric,
	string Unit,
	int Points,
	double Min,
	double Max,
	double Average,
	DateTimeOffset From,
	DateTimeOffset To,
	IReadOnlyList<HistoryPoint> Samples);

public sealed record HistoryInfoResponse(IReadOnlyList<string> Metrics, int RetentionDays, int SampleMinutes, long Bytes);

/// Deterministic reductions the application performs for the LLM
public enum AnalysisFunction
{
	Average,
	Minimum,
	Maximum,
	ValueAt,
	Trend,
	CountTransitions,
	DurationInState,
	WorstPeriod,
	Compare
}

public sealed record AnalysisRequest(
	AnalysisFunction Function,
	string Metric,
	DateTimeOffset From,
	DateTimeOffset To,
	DateTimeOffset? At = null,
	DateTimeOffset? CompareFrom = null,
	DateTimeOffset? CompareTo = null,
	double? State = null,
	int WindowMinutes = 60);

public sealed record AnalysisResult(
	AnalysisFunction Function,
	string Metric,
	string Unit,
	double? Value,
	DateTimeOffset? At,
	int Samples,
	string Summary);

public sealed record BaselineResult(
	string Metric,
	string Unit,
	int Hour,
	double? Median,
	double? Spread,
	double? Current,
	double? Deviation,
	int Samples,
	string Summary);

public sealed record CorrelationResult(
	string Metric,
	string Against,
	int Samples,
	double? Coefficient,
	string Summary);

public sealed record SessionInsight(
	ActivityCategory Category,
	DateTimeOffset Since,
	TimeSpan Length,
	string Metric,
	double? Start,
	double? Current,
	double? Delta,
	string Summary);

/// One row per day: the few numbers a rhythm is made of, kept so months of them stay cheap to read
public sealed record DaySummary(
	DateOnly Day,
	DayOfWeek Weekday,
	int? FirstPresence,
	int? LastPresence,
	int? ComputerOn,
	int? ComputerOff,
	int? SleepAt,
	double PresenceMinutes,
	double ComputerMinutes,
	double MusicMinutes,
	IReadOnlyDictionary<string, double> ActivityMinutes,
	IReadOnlyDictionary<string, double> DeviceMinutes,
	IReadOnlyDictionary<string, double> SensorAverages);

public sealed record RhythmView(
	string Metric,
	int? Usual,
	int? Today,
	int Days,
	string Summary);

public sealed record DaySummaryListResponse(IReadOnlyList<DaySummary> Days);
