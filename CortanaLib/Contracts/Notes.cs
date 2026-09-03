namespace CortanaLib.Contracts;

public enum NoteKind
{
	Personal,
	Feature,
	Link
}

public sealed record Note(
	string Id,
	string Text,
	NoteKind Kind,
	string Source,
	DateTimeOffset CreatedAt,
	bool Done = false,
	DateTimeOffset? DoneAt = null);

public sealed record NoteListResponse(IReadOnlyList<Note> Notes);

public sealed record NoteRequest(string Text, NoteKind Kind = NoteKind.Personal, string Source = "");
