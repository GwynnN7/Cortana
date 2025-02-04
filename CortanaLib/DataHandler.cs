using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib.Structures;

namespace CortanaLib;

public static class DataHandler
{
	private static readonly string CortanaPath = Env("CORTANA_PATH");
	private static readonly Dictionary<EDirType, string> Folders = new()
	{
		{ EDirType.Config, Path.Combine(CortanaPath, ".config/") },
		{ EDirType.Log, Path.Combine(CortanaPath, ".log/") },
		{ EDirType.Storage, Path.Combine(CortanaPath, "Storage/") },
		{ EDirType.Projects, CortanaPath }
	};
	
	public static readonly JsonSerializerOptions SerializerOptions;

	static DataHandler()
	{
		foreach ((EDirType type, string path) in Folders)
		{
			if(!Path.Exists(path)) throw new CortanaException($"Could not find {type} path");
		}
		
		SerializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() }
		};
	}
	
	public static string Env(string env) => Environment.GetEnvironmentVariable(env) ?? throw new EnvironmentException(env);

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

	public static string Log(string fileName, string log)
	{
		string logPath = GetPath(EDirType.Log,$"{fileName}.log");
		using StreamWriter logFile = File.Exists(logPath) ? File.AppendText(logPath) : File.CreateText(logPath);
		logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
		return log;
	}
	
	public static string GetPath(EDirType type, string path = "") => Path.Combine(Folders[type], path);
}
