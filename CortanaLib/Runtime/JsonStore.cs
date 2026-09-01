using System.Text.Json;

namespace CortanaLib.Runtime;

/// Small atomic JSON file store for every persistent repository in the Kernel
public static class JsonStore
{
	private static readonly Lock Gate = new();

	public static T? Read<T>(string path)
	{
		lock (Gate)
		{
			if (!File.Exists(path)) return default;

			try
			{
				return JsonSerializer.Deserialize<T>(File.ReadAllText(path), CortanaEnvironment.FileJson);
			}
			catch (Exception ex)
			{
				Log.Write("Storage", $"Could not read '{path}': {ex.Message}");
				return default;
			}
		}
	}

	public static T ReadOrNew<T>(string path) where T : new() => Read<T>(path) ?? new T();

	public static bool Write<T>(string path, T value)
	{
		lock (Gate)
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);

				string temporary = path + ".tmp";
				File.WriteAllText(temporary, JsonSerializer.Serialize(value, CortanaEnvironment.FileJson));
				File.Move(temporary, path, overwrite: true);
				return true;
			}
			catch (Exception ex)
			{
				Log.Write("Storage", $"Could not write '{path}': {ex.Message}");
				return false;
			}
		}
	}
}
