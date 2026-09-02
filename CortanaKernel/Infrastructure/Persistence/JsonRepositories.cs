using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Scheduling;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Domain.Volition;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Persistence;

internal static class KernelFiles
{
	public static string Path(string name) => CortanaEnvironment.Path_(CortanaFolder.Config, $"CortanaKernel/{name}");
}

public sealed class JsonSettingsRepository : ISettingsRepository
{
	private static readonly string File = KernelFiles.Path("Settings.json");

	public IReadOnlyDictionary<SettingKey, string> Load() =>
		JsonStore.Read<Dictionary<SettingKey, string>>(File) ?? new Dictionary<SettingKey, string>();

	public void Save(IReadOnlyDictionary<SettingKey, string> values) => JsonStore.Write(File, values);
}

public sealed class JsonAiSettingsRepository : IAiSettingsRepository
{
	private static readonly string File = KernelFiles.Path("Ai.json");

	private sealed record Stored(Dictionary<AiSettingKey, double> Values, string Model)
	{
		public Stored() : this([], nameof(LlmFamily.FlashLite)) { }
	}

	public IReadOnlyDictionary<AiSettingKey, double> Load() => JsonStore.ReadOrNew<Stored>(File).Values;

	public string LoadModel() => JsonStore.ReadOrNew<Stored>(File).Model;

	public void Save(IReadOnlyDictionary<AiSettingKey, double> values, string model) =>
		JsonStore.Write(File, new Stored(values.ToDictionary(entry => entry.Key, entry => entry.Value), model));
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

public sealed class JsonVolitionRepository : IVolitionRepository
{
	private static readonly string Store = KernelFiles.Path("Volition.json");

	public VolitionState Load() => JsonStore.Read<VolitionState>(Store) ?? new VolitionState();

	public void Save(VolitionState state) => JsonStore.Write(Store, state);
}
