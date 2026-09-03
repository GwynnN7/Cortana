using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Notes;

public interface INoteRepository
{
	IReadOnlyList<Note> Load();
	void Save(IReadOnlyList<Note> notes);
}

/// Things to act on later, written down by gwynn7 or by Cortana on his behalf
public sealed class NoteStore(INoteRepository repository)
{
	private const int Capacity = 500;

	private readonly List<Note> _notes = [.. repository.Load()];
	private readonly Lock _gate = new();

	public IReadOnlyList<Note> All()
	{
		lock (_gate) return [.. _notes.OrderBy(note => note.Done).ThenByDescending(note => note.CreatedAt)];
	}

	public IReadOnlyList<Note> Open()
	{
		lock (_gate) return [.. _notes.Where(note => !note.Done).OrderByDescending(note => note.CreatedAt)];
	}

	public Result<Note> Write(string text, NoteKind kind, string source)
	{
		string trimmed = (text ?? "").ReplaceLineEndings(" ").Trim();

		if (trimmed.Length == 0) return Result.Fail<Note>("Nothing to write down");
		if (trimmed.Length > 500) return Result.Fail<Note>("That is too long for a note");

		lock (_gate)
		{
			if (_notes.FirstOrDefault(note => !note.Done && string.Equals(note.Text, trimmed, StringComparison.OrdinalIgnoreCase)) is { } existing)
				return Result.Fail<Note>($"Already written down on {existing.CreatedAt:dd MMM}");

			var note = new Note(Guid.NewGuid().ToString("N")[..8], trimmed, kind, source, DateTimeOffset.Now);
			_notes.Add(note);

			while (_notes.Count > Capacity)
				_notes.Remove(_notes.Where(entry => entry.Done).OrderBy(entry => entry.DoneAt).FirstOrDefault() ?? _notes[0]);

			repository.Save(_notes);
			return Result.Ok(note);
		}
	}

	public Result<Note> Settle(string id, bool done)
	{
		lock (_gate)
		{
			int index = _notes.FindIndex(note => note.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (index < 0) return Result.Fail<Note>($"No note with id '{id}'");

			Note settled = _notes[index] with { Done = done, DoneAt = done ? DateTimeOffset.Now : null };
			_notes[index] = settled;

			repository.Save(_notes);
			return Result.Ok(settled);
		}
	}

	public Result<string> Drop(string id)
	{
		lock (_gate)
		{
			int removed = _notes.RemoveAll(note => note.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			if (removed == 0) return Result.Fail<string>($"No note with id '{id}'");

			repository.Save(_notes);
			return Result.Ok("Dropped");
		}
	}

	public int Clear()
	{
		lock (_gate)
		{
			int removed = _notes.RemoveAll(note => note.Done);
			if (removed > 0) repository.Save(_notes);
			return removed;
		}
	}
}
