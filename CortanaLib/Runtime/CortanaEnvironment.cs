using System.Text.Json;
using System.Text.Json.Serialization;

namespace CortanaLib.Runtime;

public enum CortanaFolder
{
	Config,
	Storage,
	Temp
}

/// Process-wide configuration: the `.env` file, the config/storage folders and the JSON
public static class CortanaEnvironment
{
	private static readonly Dictionary<CortanaFolder, string> Folders;

	public static readonly JsonSerializerOptions FileJson;
	public static readonly JsonSerializerOptions WireJson;

	static CortanaEnvironment()
	{
		string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "cortana");

		Folders = new Dictionary<CortanaFolder, string>
		{
			[CortanaFolder.Config] = config,
			[CortanaFolder.Storage] = Path.Combine(AppContext.BaseDirectory, "Storage"),
			[CortanaFolder.Temp] = Path.Combine(Path.GetTempPath(), "cortana")
		};

		Directory.CreateDirectory(Folders[CortanaFolder.Temp]);

		FileJson = new JsonSerializerOptions
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			PropertyNameCaseInsensitive = true,
			Converters = { new JsonStringEnumConverter() }
		};

		WireJson = new JsonSerializerOptions(FileJson) { WriteIndented = false };
	}

	public static string Path_(CortanaFolder folder, string relative = "") => Path.Combine(Folders[folder], relative);

	public static string Require(string variable) =>
		Environment.GetEnvironmentVariable(variable) ?? throw new CortanaException($"{variable} is not set in the environment");

	public static string? Read(string variable) => Environment.GetEnvironmentVariable(variable);

	public static string Read(string variable, string fallback) => Environment.GetEnvironmentVariable(variable) ?? fallback;

	public static void Load(bool required = true)
	{
		string file = Path_(CortanaFolder.Config, ".env");
		if (!File.Exists(file))
		{
			if (required) throw new CortanaException($"Cannot load the environment file '{file}'");
			return;
		}

		foreach (string line in File.ReadAllLines(file))
		{
			string trimmed = line.Trim();
			if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

			string[] parts = trimmed.Split('=', 2);
			if (parts.Length != 2) continue;

			string key = parts[0].Trim();
			if (Environment.GetEnvironmentVariable(key) == null)
				Environment.SetEnvironmentVariable(key, parts[1].Trim().Trim('"'));
		}
	}
}

public sealed class CortanaException(string message) : Exception(message);
