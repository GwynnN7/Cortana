using CortanaLib.Contracts;

namespace CortanaKernel.Domain.History;

/// One recorded row of everything worth plotting
public sealed record HistorySample(DateTimeOffset At, IReadOnlyDictionary<string, double?> Values);

public interface IHistoryRepository
{
	IReadOnlyList<string> Metrics { get; }
	void Append(HistorySample sample);
	IReadOnlyList<HistoryPoint> Read(string metric, DateTimeOffset from, DateTimeOffset to);
	void Prune(int retentionDays);
	long DiskUsage();
}
