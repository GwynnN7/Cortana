﻿using System.Text.Json;
using System.Text.Json.Serialization;
using CortanaLib.Structures;

namespace CortanaLib;

public static class DataHandler
{
	private static readonly string CortanaFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "Cortana");
	private static readonly Dictionary<EDirType, string> Folders;
	public static readonly JsonSerializerOptions SerializerOptions;

	static DataHandler()
	{
		if (!Directory.Exists(CortanaFolder)) throw new CortanaException("Could not find .config folder");
		
		Folders = new Dictionary<EDirType, string>
		{
			{ EDirType.Config, Path.Combine(CortanaFolder, "Config/") },
			{ EDirType.Log, Path.Combine(CortanaFolder, "Log/") },
			{ EDirType.Storage, Path.Combine(CortanaFolder, "Storage/") }
		};
		
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

	public static string Log(string fileName, string log)
	{
		string logPath = CortanaPath(EDirType.Log,$"{fileName}.log");
		using StreamWriter logFile = File.Exists(logPath) ? File.AppendText(logPath) : File.CreateText(logPath);
		logFile.WriteLine($"{DateTime.Now}\n{log}\n------\n\n");
		return log;
	}
}
