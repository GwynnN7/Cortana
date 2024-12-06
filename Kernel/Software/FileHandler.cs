using Kernel.Software.Utility;
using Newtonsoft.Json;

namespace Kernel.Software;

public static class FileHandler
{
	public static readonly Secrets Secrets;
	public static readonly string ProjectStoragePath;

	static FileHandler()
	{
		ProjectStoragePath = Path.Combine(Environment.CurrentDirectory, "Storage/");
		if(!Path.Exists(ProjectStoragePath)) throw new CortanaException("Could not find storage path");
		
		Secrets = LoadFile<Secrets>(Path.Combine(Environment.CurrentDirectory, "Config/Secrets.json"));
	}

	public static void WriteFile<T>(string fileName, T data, JsonSerializerSettings? options = null)
	{
		options ??= new JsonSerializerSettings { Formatting = Formatting.Indented };
		string newJson = JsonConvert.SerializeObject(data, options);
		string filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
		File.WriteAllText(filePath, newJson);
	}
	
	public static T? LoadFile<T>(string path)
	{
		T? dataToLoad = default;
		if (!File.Exists(path)) return dataToLoad;

		try
		{
			string file = File.ReadAllText(path);
			dataToLoad = JsonConvert.DeserializeObject<T>(file);
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

