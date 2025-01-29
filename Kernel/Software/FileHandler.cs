using System.Text.Json;
using System.Text.Json.Serialization;
using Kernel.Software.DataStructures;

namespace Kernel.Software;

public static class FileHandler
{
	public static readonly Secrets Secrets;
	public static readonly string ProjectStoragePath;
	internal static readonly JsonSerializerOptions SerializerOptions;

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
		
		Secrets = Deserialize<Secrets>(Path.Combine(ProjectStoragePath, "Config/Secrets.json"));
	}

	public static void SerializeObject<T>(T obj, string path)
	{
		string newJson = JsonSerializer.Serialize(obj, SerializerOptions);
		string filePath = Path.Combine(Directory.GetCurrentDirectory(), path);
		File.WriteAllText(filePath, newJson);
	}

	public static T? Deserialize<T>(string path)
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

	public static string LoadHtml(string name)
	{
		string filePath = Path.Combine(ProjectStoragePath, $"Assets/HTML/{name}.html");
		try
		{
			return File.ReadAllText(filePath);
		}
		catch (Exception ex)
		{
			throw new CortanaException(ex.Message, ex);
		}
	}

	public static void Log(string fileName, string log)
	{
		string logPath = Path.Combine(ProjectStoragePath, $"Log/{fileName}.log");
		using StreamWriter logFile = File.Exists(logPath) ? File.AppendText(logPath) : File.CreateText(logPath);
		logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
	}
}
