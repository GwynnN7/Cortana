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

	/// How long the wrap-ups stay. Long enough to notice a rhythm, short enough not to become history
	private static readonly TimeSpan DayLifetime = TimeSpan.FromDays(7);

	private readonly List<MemoryEntry> _memories = [.. repository.Load()];
	private readonly Lock _gate = new();

	/// An expired memory is not a memory. Pruning only runs at midnight, and until it did, a state that
	/// had already lapsed still showed on the Memory page and read as something she currently believes
	public IReadOnlyList<MemoryEntry> All()
	{
		DateTimeOffset now = DateTimeOffset.Now;

		lock (_gate)
			return [.. _memories.Where(memory => memory.ExpiresAt is not { } expires || expires > now)
				.OrderByDescending(memory => memory.CreatedAt)];
	}

	public Result<MemoryEntry> Remember(string text, MemoryKind kind, string source, TimeSpan stateLifetime)
	{
		string trimmed = (text ?? "").ReplaceLineEndings(" ").Trim();
		if (trimmed.Length == 0) return Result.Fail<MemoryEntry>("Nothing to remember");
		if (trimmed.Length > 400) return Result.Fail<MemoryEntry>("That is too long to remember, keep it to a sentence");

		DateTimeOffset now = DateTimeOffset.Now;

		lock (_gate)
		{
			// A lapsed memory is not one she holds, and All() no longer shows it, so leaving it sitting in
			// front of the duplicate check would block that sentence for ever - and with no visible id,
			// nothing could forget it either. Pruning here rather than only at midnight closes that trap
			_memories.RemoveAll(memory => memory.ExpiresAt is { } lapsed && lapsed <= now);

			// There is only ever one state, because there is only one place he currently is. Evicting
			// before the duplicate check is what lets him repeat himself: "still away" an hour later has
			// to push the expiry out, not be turned away as something already known.
			// A day is not a state - it used to be stored as one, and every evening's wrap-up quietly
			// deleted whatever he had said about being away, which is exactly what the morning greeting needed
			if (kind == MemoryKind.State) _memories.RemoveAll(memory => memory.Kind == MemoryKind.State);

			if (_memories.FirstOrDefault(memory => string.Equals(memory.Text, trimmed, StringComparison.OrdinalIgnoreCase)) is { } existing)
				return Result.Fail<MemoryEntry>($"Already remembered that on {existing.CreatedAt:dd MMM}");

			DateTimeOffset? expires = kind switch
			{
				MemoryKind.State => now + stateLifetime,
				MemoryKind.Day => now + DayLifetime,
				_ => null
			};

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
