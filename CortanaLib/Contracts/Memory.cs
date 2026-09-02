namespace CortanaLib.Contracts;

public enum MemoryKind
{
	Fact,
	Preference,
	Event,
	State
}

public sealed record MemoryEntry(
	string Id,
	string Text,
	MemoryKind Kind,
	string Source,
	DateTimeOffset CreatedAt,
	DateTimeOffset LastUsedAt,
	int Uses,
	DateTimeOffset? ExpiresAt = null);

public sealed record MemoryListResponse(IReadOnlyList<MemoryEntry> Memories);

public sealed record RememberRequest(string Text, MemoryKind Kind = MemoryKind.Fact, string Source = "");
