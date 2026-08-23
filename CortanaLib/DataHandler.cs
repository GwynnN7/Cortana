using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib.Structures;

namespace CortanaLib;

public static class DataHandler
{
	private static readonly string CortanaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "cortana");
	private static readonly Dictionary<EDirType, string> Folders;
	public static readonly JsonSerializerOptions SerializerOptions;
	public static readonly JsonSerializerOptions ApiSerializerOptions;

	static DataHandler()
	{
		if (!Directory.Exists(CortanaFolder)) throw new CortanaException($"Could not find configuration folder '{CortanaFolder}'");
		Folders = new Dictionary<EDirType, string>
		{
			{ EDirType.Config, CortanaFolder },
			{ EDirType.Storage, Path.Combine(AppContext.BaseDirectory, "Storage") },
			{ EDirType.Temp, Path.Combine(Path.GetTempPath(), "cortana") }
		};

		Directory.CreateDirectory(Folders[EDirType.Temp]);

		SerializerOptions = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() }
		};

		ApiSerializerOptions = new JsonSerializerOptions(SerializerOptions) { WriteIndented = false };
	}

	public static string Env(string env) => Environment.GetEnvironmentVariable(env) ?? throw new EnvironmentException(env);
	public static string? EnvOrNull(string env) => Environment.GetEnvironmentVariable(env);
	public static string CortanaPath(EDirType type, string path = "") => Path.Combine(Folders[type], path);

		public static void LoadEnvironment(bool required = true)
	{
		string filePath = CortanaPath(EDirType.Config, ".env");
		if (!File.Exists(filePath))
		{
			if (required) throw new CortanaException($"Cannot load environment file '{filePath}', quitting...");
			return;
		}

		foreach (string line in File.ReadAllLines(filePath))
		{
			string trimmed = line.Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

			string[] parts = trimmed.Split('=', 2);
			if (parts.Length != 2) continue;

			string key = parts[0].Trim();
			string value = parts[1].Trim().Trim('"');
			if (Environment.GetEnvironmentVariable(key) == null) Environment.SetEnvironmentVariable(key, value);
		}
	}

	public static T? DeserializeJson<T>(string path)
	{
		if (!File.Exists(path)) return default;

		try
		{
			string file = File.ReadAllText(path);
			return JsonSerializer.Deserialize<T>(file, SerializerOptions);
		}
		catch (Exception ex)
		{
			throw new CortanaException($"Could not read '{path}': {ex.Message}");
		}
	}

	public static string Log(string log)
	{
		Console.WriteLine(log);
		return log;
	}
}
