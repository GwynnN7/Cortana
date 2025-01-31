using System.Text.Json;
using System.Text.Json.Serialization;
using Utility.Structures;

namespace Utility;

public static class FileHandler
{
	public static readonly Secrets Secrets;
	public static readonly string ProjectStoragePath;
	public static readonly JsonSerializerOptions SerializerOptions;

	static FileHandler()
	{
		ProjectStoragePath = Path.Combine(Environment.CurrentDirectory, "Storage/");
		if(!Path.Exists(ProjectStoragePath)) throw new CortanaException("Could not find storage path");
		
		SerializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
			PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
			Converters = { new JsonStringEnumConverter() }
		};
		
		Secrets = DeserializeJson<Secrets>(GetPath("Config/Secrets.json"));
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
		string logPath = GetPath($".log/{fileName}.log");
		using StreamWriter logFile = File.Exists(logPath) ? File.AppendText(logPath) : File.CreateText(logPath);
		logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
	}
	
	private static string GetPath(string path) => Path.Combine(ProjectStoragePath, path);
}
