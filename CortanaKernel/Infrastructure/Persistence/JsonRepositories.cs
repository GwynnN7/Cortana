using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Scheduling;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.History;
using CortanaKernel.Domain.Notes;
using CortanaKernel.Domain.Volition;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using System.Text.Json.Nodes;

namespace CortanaKernel.Infrastructure.Persistence;

internal static class KernelFiles
{
	public static string Path(string name) => CortanaEnvironment.Path_(CortanaFolder.Config, $"CortanaKernel/{name}");
}

public sealed class JsonSettingsRepository : ISettingsRepository
{
	private static readonly string File = KernelFiles.Path("Settings.json");

	public IReadOnlyDictionary<SettingKey, string> Load()
	{
		Dictionary<string, string> stored = JsonStore.Read<Dictionary<string, string>>(File) ?? [];
		var values = new Dictionary<SettingKey, string>();

		foreach ((string name, string value) in stored)
			if (Enum.TryParse(name, true, out SettingKey key)) values[key] = value;
			else Log.Write("Settings", $"Ignoring '{name}', it is no longer a setting");

		return values;
	}

	public void Save(IReadOnlyDictionary<SettingKey, string> values) => JsonStore.Write(File, values);
}

public sealed class JsonAiSettingsRepository : IAiSettingsRepository
{
	private static readonly string File = KernelFiles.Path("Ai.json");

	private sealed record Stored(Dictionary<string, double> Values, string Model)
	{
		public Stored() : this([], nameof(LlmFamily.FlashLite)) { }
	}

	/// Keys are read one by one: a setting that no longer exists is skipped, never taking the file with it
	public IReadOnlyDictionary<AiSettingKey, double> Load()
	{
		var values = new Dictionary<AiSettingKey, double>();

		foreach ((string name, double value) in JsonStore.ReadOrNew<Stored>(File).Values)
			if (Enum.TryParse(name, out AiSettingKey key)) values[key] = value;
			else Log.Write("Settings", $"Ignoring '{name}', which is no longer an AI setting");

		return values;
	}

	public string LoadModel() => JsonStore.ReadOrNew<Stored>(File).Model;

	public void Save(IReadOnlyDictionary<AiSettingKey, double> values, string model) =>
		JsonStore.Write(File, new Stored(values.ToDictionary(entry => entry.Key.ToString(), entry => entry.Value), model));
}

public sealed class JsonScheduleRepository : IScheduleRepository
{
	private static readonly string File = KernelFiles.Path("Schedules.json");

	public IReadOnlyList<Schedule> Load() => JsonStore.Read<List<Schedule>>(File) ?? [];

	public void Save(IReadOnlyList<Schedule> schedules) => JsonStore.Write(File, schedules);
}

/// One file per conversation, so `chat` survives a Kernel restart and conversations stay independent
public sealed class JsonConversationRepository : IConversationRepository
{
	private static readonly string Folder = KernelFiles.Path("Conversations");
	private static readonly TimeSpan IdleExpiry = TimeSpan.FromDays(30);

	public JsonConversationRepository()
	{
		Directory.CreateDirectory(Folder);
		Prune();
	}

	public Conversation? Load(string id) => JsonStore.Read<Conversation>(PathFor(id));

	public void Save(Conversation conversation) => JsonStore.Write(PathFor(conversation.Id), conversation);

	public void Delete(string id)
	{
		try
		{
			string path = PathFor(id);
			if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
		}
		catch (Exception ex)
		{
			Log.Error("Conversations", $"Could not delete '{id}': {ex.Message}");
		}
	}

	public IReadOnlyList<string> Ids()
	{
		try
		{
			return [.. Directory.EnumerateFiles(Folder, "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).OfType<string>()];
		}
		catch (Exception)
		{
			return [];
		}
	}

	private void Prune()
	{
		try
		{
			DateTime cutoff = DateTime.UtcNow - IdleExpiry;
			foreach (string file in Directory.EnumerateFiles(Folder, "*.json"))
				if (System.IO.File.GetLastWriteTimeUtc(file) < cutoff) System.IO.File.Delete(file);
		}
		catch (Exception ex)
		{
			Log.Error("Conversations", $"Could not prune: {ex.Message}");
		}
	}

	/// Conversation ids come from clients ("web:...", "discord:123"), sanitised into filenames
	private static string PathFor(string id) =>
		System.IO.Path.Combine(Folder, string.Concat(id.Select(character =>
			char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_')) + ".json");
}

public sealed class JsonMemoryRepository : IMemoryRepository
{
	private static readonly string Store = KernelFiles.Path("Memory.json");

	public IReadOnlyList<MemoryEntry> Load() => JsonStore.Read<List<MemoryEntry>>(Store) ?? [];

	public void Save(IReadOnlyList<MemoryEntry> memories) => JsonStore.Write(Store, memories);
}

/// One JSON array of days. A year is a few hundred small rows, so it is read whole
public sealed class JsonRhythmRepository : IRhythmRepository
{
	private static readonly string Store = KernelFiles.Path("Days.json");
	private const int Keep = 400;

	private readonly Lock _gate = new();

	public IReadOnlyList<DaySummary> Load(int days)
	{
		lock (_gate)
		{
			List<DaySummary> all = JsonStore.Read<List<DaySummary>>(Store) ?? [];
			return [.. all.OrderByDescending(day => day.Day).Take(Math.Max(1, days))];
		}
	}

	public void Save(DaySummary day)
	{
		lock (_gate)
		{
			List<DaySummary> all = JsonStore.Read<List<DaySummary>>(Store) ?? [];

			all.RemoveAll(entry => entry.Day == day.Day);
			all.Add(day);

			JsonStore.Write(Store, all.OrderByDescending(entry => entry.Day).Take(Keep).OrderBy(entry => entry.Day).ToList());
		}
	}
}

public sealed class JsonNoteRepository : INoteRepository
{
	private static readonly string Store = KernelFiles.Path("Notes.json");

	public IReadOnlyList<Note> Load() => JsonStore.Read<List<Note>>(Store) ?? [];

	public void Save(IReadOnlyList<Note> notes) => JsonStore.Write(Store, notes);
}

public sealed class JsonVolitionRepository : IVolitionRepository
{
	private static readonly string Store = KernelFiles.Path("Volition.json");

	public VolitionState Load() => JsonStore.Read<VolitionState>(Store) ?? new VolitionState();

	public void Save(VolitionState state) => JsonStore.Write(Store, state);
}

public sealed class JsonFabricRepository : IFabricRepository
{
	private static readonly string Sources = KernelFiles.Path("Sources.json");
	private static readonly string Registered = KernelFiles.Path("Registrations.json");
	private static readonly string Channels = KernelFiles.Path("Channels.json");

	public IReadOnlyList<SourceDescriptor> LoadSources() => JsonStore.Read<List<SourceDescriptor>>(Sources) ?? [];

	public void SaveSources(IReadOnlyList<SourceDescriptor> sources) => JsonStore.Write(Sources, sources);

	public Registrations LoadRegistrations()
	{
		UpgradePresence();
		return JsonStore.Read<Registrations>(Registered) ?? new Registrations([], []);
	}

	/// Presence used to be one bool, which could only ever mean "reports somebody is here". A file
	/// written before the split still says `feedsPresence`, and dropping it silently would leave the
	/// house with no presence at all, so it is carried over once. Which sensors should only *sustain*
	/// presence is a judgement about the room, not something to infer here - that is set on the
	/// Hardware page, and the shipped defaults already say it for a fresh install
	private static void UpgradePresence()
	{
		if (!File.Exists(Registered)) return;

		try
		{
			if (JsonNode.Parse(File.ReadAllText(Registered)) is not JsonObject root) return;
			if (root["sensors"] is not JsonArray sensors) return;

			var carried = 0;

			foreach (JsonNode? node in sensors)
			{
				if (node is not JsonObject sensor) continue;
				if (sensor.ContainsKey("presence") || !sensor.TryGetPropertyValue("feedsPresence", out JsonNode? legacy)) continue;

				sensor.Remove("feedsPresence");
				sensor["presence"] = (legacy?.GetValue<bool>() ?? false ? PresenceRole.Reports : PresenceRole.None).ToString();
				carried++;
			}

			if (carried == 0) return;

			JsonStore.Write(Registered, root);
			Log.Write("Storage", $"Carried {carried} sensor{(carried == 1 ? "" : "s")} over to the new presence roles");
		}
		catch (Exception ex)
		{
			Log.Write("Storage", $"Could not carry the presence flags over: {ex.Message}");
		}
	}

	public void SaveRegistrations(Registrations registrations) => JsonStore.Write(Registered, registrations);

	public IReadOnlyDictionary<string, PowerState> LoadChannels() =>
		JsonStore.Read<Dictionary<string, PowerState>>(Channels) ?? [];

	public void SaveChannels(IReadOnlyDictionary<string, PowerState> channels) =>
		JsonStore.Write(Channels, channels);
}

public sealed class JsonBindRepository : IBindRepository
{
	private static readonly string Store = KernelFiles.Path("Binds.json");

	public IReadOnlyList<Bind> Load() => JsonStore.Read<List<Bind>>(Store) ?? FabricDefaults.Binds;

	public void Save(IReadOnlyList<Bind> binds) => JsonStore.Write(Store, binds);
}

public sealed class JsonWarningRepository : IWarningRepository
{
	private static readonly string Store = KernelFiles.Path("Warnings.json");

	public IReadOnlyList<Warning> Load() => JsonStore.Read<List<Warning>>(Store) ?? [];

	public void Save(IReadOnlyList<Warning> warnings) => JsonStore.Write(Store, warnings);
}

public sealed class JsonLayoutRepository
{
	private static readonly string Store = KernelFiles.Path("Layout.json");

	public DashboardLayout Load() => JsonStore.Read<DashboardLayout>(Store) ?? new DashboardLayout([], []);

	public void Save(DashboardLayout layout) => JsonStore.Write(Store, layout);
}
