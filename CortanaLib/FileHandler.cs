using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib.Structures;

namespace CortanaLib;

public static class FileHandler
{
	public static readonly Secrets Secrets;

	private static readonly Dictionary<EDirType, string> Folders = new()
	{
		{ EDirType.Config, Path.Combine(Environment.CurrentDirectory, ".config/") },
		{ EDirType.Storage, Path.Combine(Environment.CurrentDirectory, "Storage/") },
		{ EDirType.Log, Path.Combine(Environment.CurrentDirectory, ".log/") }
	};
	
	public static readonly JsonSerializerOptions SerializerOptions;

	static FileHandler()
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
		
		Secrets = DeserializeJson<Secrets>(GetPath(EDirType.Config,"Secrets.json"));
	}

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
			throw new CortanaException(ex.Message, ex);
		}
		return dataToLoad;
	}

	public static void Log(string fileName, string log)
	{
		string logPath = GetPath(EDirType.Log,$"{fileName}.log");
		using StreamWriter logFile = File.Exists(logPath) ? File.AppendText(logPath) : File.CreateText(logPath);
		logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
	}
	
	public static string GetPath(EDirType type, string path) => Path.Combine(Folders[type], path);
}
