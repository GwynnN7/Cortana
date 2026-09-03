using CortanaKernel.Domain.Notes;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Api.Endpoints;

public static class NoteEndpoints
{
	public static void MapNoteEndpoints(this IEndpointRouteBuilder app)
	{
		RouteGroupBuilder group = app.MapGroup("/notes").WithTags("Notes");

		group.MapGet("", (NoteStore store, SettingsStore flags, HttpRequest request) =>
			{
				if (!flags.Flag(SettingKey.NotesEnabled)) return ApiResults.Unavailable(request, "Notes are switched off");

				IReadOnlyList<Note> all = store.All();
				string text = all.Count == 0
					? "Nothing written down"
					: string.Join("\n", all.Select(note =>
						$"{(note.Done ? "done" : "open")} [{note.Id}] {note.Kind.ToString().ToLowerInvariant()}: {note.Text}"));

				return ApiResults.Ok(request, text, new NoteListResponse(all));
			})
			.Access(ApiAccess.ReadOnly).WithSummary("Everything written down").Produces<NoteListResponse>();

		group.MapPost("", (NoteRequest body, NoteStore store, SettingsStore flags, HttpRequest request) =>
				flags.Flag(SettingKey.NotesEnabled)
					? ApiResults.From(request, store.Write(body.Text, body.Kind, body.Source), note => $"Written down as {note.Id}")
					: ApiResults.Unavailable(request, "Notes are switched off"))
			.Access(ApiAccess.Sensitive).WithSummary("Writes a note down");

		group.MapPost("/{id}/done", (string id, NoteStore store, HttpRequest request) =>
				ApiResults.From(request, store.Settle(id, true), note => $"'{note.Text}' is done"))
			.Access(ApiAccess.Sensitive).WithSummary("Marks a note done");

		group.MapPost("/{id}/open", (string id, NoteStore store, HttpRequest request) =>
				ApiResults.From(request, store.Settle(id, false), note => $"'{note.Text}' is open again"))
			.Access(ApiAccess.Sensitive).WithSummary("Reopens a note");

		group.MapDelete("/{id}", (string id, NoteStore store, HttpRequest request) =>
				ApiResults.From(request, store.Drop(id)))
			.Access(ApiAccess.Sensitive).WithSummary("Drops a note");

		group.MapDelete("", (NoteStore store, HttpRequest request) =>
				ApiResults.Message(request, $"{store.Clear()} finished note(s) cleared"))
			.Access(ApiAccess.Sensitive).WithSummary("Clears every finished note");
	}
}
