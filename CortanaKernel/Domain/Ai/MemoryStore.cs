using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Ai;

public interface IMemoryRepository
{
	IReadOnlyList<MemoryEntry> Load();
	void Save(IReadOnlyList<MemoryEntry> memories);
}

public sealed class MemoryStore(IMemoryRepository repository)
{
	private const int Capacity = 300;

	private readonly List<MemoryEntry> _memories = [.. repository.Load()];
	private readonly Lock _gate = new();

	public IReadOnlyList<MemoryEntry> All()
	{
		lock (_gate) return [.. _memories.OrderByDescending(memory => memory.CreatedAt)];
	}

	public Result<MemoryEntry> Remember(string text, MemoryKind kind, string source, TimeSpan stateLifetime)
	{
		string trimmed = (text ?? "").ReplaceLineEndings(" ").Trim();
		if (trimmed.Length == 0) return Result.Fail<MemoryEntry>("Nothing to remember");
		if (trimmed.Length > 400) return Result.Fail<MemoryEntry>("That is too long to remember, keep it to a sentence");

		DateTimeOffset now = DateTimeOffset.Now;

		lock (_gate)
		{
			if (_memories.FirstOrDefault(memory => string.Equals(memory.Text, trimmed, StringComparison.OrdinalIgnoreCase)) is { } existing)
				return Result.Fail<MemoryEntry>($"Already remembered that on {existing.CreatedAt:dd MMM}");

			if (kind == MemoryKind.State) _memories.RemoveAll(memory => memory.Kind == MemoryKind.State);

			DateTimeOffset? expires = kind == MemoryKind.State ? now + stateLifetime : null;

			var entry = new MemoryEntry(Guid.NewGuid().ToString("N")[..8], trimmed, kind, source, now, now, 0, expires);
			_memories.Add(entry);

			if (_memories.Count > Capacity)
				_memories.RemoveAll(memory => memory.Id == Weakest(now).Id);

			repository.Save(_memories);
			return Result.Ok(entry);
		}
	}

	public IReadOnlyList<MemoryEntry> Recall(string query, int limit)
	{
		if (limit <= 0) return [];

		string[] words = [.. query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
		DateTimeOffset now = DateTimeOffset.Now;

		lock (_gate)
		{
			List<MemoryEntry> found =
			[
				.. _memories
					.Where(memory => memory.ExpiresAt is not { } expires || expires > now)
					.OrderByDescending(memory => Score(memory, words, now))
					.Take(limit)
			];

			for (var i = 0; i < found.Count; i++)
			{
				MemoryEntry used = found[i] with { LastUsedAt = now, Uses = found[i].Uses + 1 };
				_memories[_memories.FindIndex(memory => memory.Id == used.Id)] = used;
				found[i] = used;
			}

			if (found.Count > 0) repository.Save(_memories);
			return found;
		}
	}

	public Result<string> Forget(string id)
	{
		lock (_gate)
		{
			int removed = _memories.RemoveAll(memory => memory.Id == id);
			if (removed == 0) return Result.Fail<string>($"No memory with id '{id}'");

			repository.Save(_memories);
			return Result.Ok("Forgotten");
		}
	}

	public int Prune()
	{
		DateTimeOffset now = DateTimeOffset.Now;

		lock (_gate)
		{
			int removed = _memories.RemoveAll(memory => memory.ExpiresAt is { } expires && expires <= now);
			if (removed > 0) repository.Save(_memories);
			return removed;
		}
	}

	private MemoryEntry Weakest(DateTimeOffset now) =>
		_memories.OrderBy(memory => Score(memory, [], now)).First();

	private static double Score(MemoryEntry memory, string[] words, DateTimeOffset now)
	{
		double age = Math.Max((now - memory.LastUsedAt).TotalDays, 0);
		double weight = (memory.Uses + 1) / (1 + age / 30);

		if (memory.Kind == MemoryKind.Preference) weight *= 1.5;
		if (memory.Kind == MemoryKind.State) weight += 20;
		if (words.Length == 0) return weight;

		string text = memory.Text.ToLowerInvariant();
		int hits = words.Count(word => word.Length > 2 && text.Contains(word));

		return weight + hits * 4;
	}
}
