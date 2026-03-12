using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib.Structures;

namespace CortanaLib;

public static class DataHandler
{
	private static readonly string CortanaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "cortana");
	private static readonly Dictionary<EDirType, string> Folders;
	public static readonly JsonSerializerOptions SerializerOptions;

	static DataHandler()
	{
		if (!Directory.Exists(CortanaFolder)) throw new CortanaException("Could not find .config folder");
		Folders = new Dictionary<EDirType, string>
		{
			{ EDirType.Config, CortanaFolder },
			{ EDirType.Storage, Path.Combine(AppContext.BaseDirectory, "Storage") },
			{ EDirType.Temp, Path.Combine(Path.GetTempPath(), "cortana") }
		};

		Directory.CreateDirectory(Folders[EDirType.Temp]);
		
		SerializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() }
		};
	}

	public static string Env(string env) => Environment.GetEnvironmentVariable(env) ?? throw new EnvironmentException(env);
	public static string CortanaPath(EDirType type, string path = "") => Path.Combine(Folders[type], path);

	public static T? DeserializeJson<T>(string path)
	{
		T? dataToLoad = default;
		if (!File.Exists(path)) return dataToLoad;

		try
		{
			string file = File.ReadAllText(path);
			dataToLoad = JsonSerializer.Deserialize<T>(file, SerializerOptions);
		}
		catch (Exception ex)
		{
			throw new CortanaException(ex.Message);
		}
		return dataToLoad;
	}

	public static string Log(string source, string log)
	{
		Console.WriteLine($"[{source}] {log}");
		return log;
	}
}
