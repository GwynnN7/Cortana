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
