using System.Globalization;
using System.Text.Json;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class AiSettings
{
	private static readonly string Path = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/Ai.json");
	private static readonly Lock Gate = new();

	private static readonly IReadOnlyDictionary<EAiSetting, (double Min, double Max)> Bounds =
		new Dictionary<EAiSetting, (double, double)>
		{
			[EAiSetting.Temperature] = (0, 2),
			[EAiSetting.History] = (1, 40),
			[EAiSetting.DiscordMinutes] = (0.5, 120)
		};

	private sealed class Stored
	{
		public string Model { get; set; } = nameof(ELlmModel.FlashLite);
		public double Temperature { get; set; } = 0.9;
		public int History { get; set; } = 8;
		public double DiscordMinutes { get; set; } = 1;
	}

	private static Stored _values = Load();

	public static ELlmModel Model
	{
		get
		{
			lock (Gate) return Enum.TryParse(_values.Model, true, out ELlmModel model) ? model : ELlmModel.FlashLite;
		}
	}

	public static double Temperature { get { lock (Gate) return _values.Temperature; } }
	public static int History { get { lock (Gate) return _values.History; } }
	public static double DiscordMinutes { get { lock (Gate) return _values.DiscordMinutes; } }

	private static Stored Load()
	{
		try
		{
			if (File.Exists(Path))
			{
				var stored = JsonSerializer.Deserialize<Stored>(File.ReadAllText(Path), DataHandler.SerializerOptions);
				if (stored is not null) return stored;
			}
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[AI] Could not read the settings: {ex.Message}");
		}

		return Migrate();
	}

	private static Stored Migrate()
	{
		var stored = new Stored();

		string model = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "Model.txt");
		string temperature = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, "Temperature.txt");

		try
		{
			if (File.Exists(model)) stored.Model = File.ReadAllText(model).Trim();
			if (File.Exists(temperature) && double.TryParse(File.ReadAllText(temperature).Trim(), CultureInfo.InvariantCulture, out double value))
				stored.Temperature = value;
		}
		catch (IOException)
		{
		}

		return stored;
	}

	private static StringResult Save()
	{
		try
		{
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
			File.WriteAllText(Path, JsonSerializer.Serialize(_values, DataHandler.SerializerOptions));

			return StringResult.Success("Saved");
		}
		catch (Exception ex)
		{
			return StringResult.Failure($"Could not save the settings: {ex.Message}");
		}
	}

	public static StringResult SetModel(ELlmModel model)
	{
		lock (Gate)
		{
			_values.Model = model.ToString();
			return Save();
		}
	}

	public static string Read(EAiSetting setting) => setting switch
	{
		EAiSetting.Temperature => Temperature.ToString("0.##", CultureInfo.InvariantCulture),
		EAiSetting.History => History.ToString(CultureInfo.InvariantCulture),
		EAiSetting.DiscordMinutes => DiscordMinutes.ToString("0.##", CultureInfo.InvariantCulture),
		_ => ""
	};

	public static IReadOnlyList<AiSettingResponse> All() =>
		Enum.GetValues<EAiSetting>().Select(setting => new AiSettingResponse(setting.ToString(), Read(setting))).ToList();

	public static StringResult Write(EAiSetting setting, double value)
	{
		(double min, double max) = Bounds[setting];
		if (value < min || value > max)
			return StringResult.Failure($"{setting} must be between {min.ToString("0.##", CultureInfo.InvariantCulture)} and {max.ToString("0.##", CultureInfo.InvariantCulture)}");

		lock (Gate)
		{
			switch (setting)
			{
				case EAiSetting.Temperature: _values.Temperature = value; break;
				case EAiSetting.History: _values.History = (int)value; break;
				case EAiSetting.DiscordMinutes: _values.DiscordMinutes = value; break;
			}

			StringResult saved = Save();
			return saved.IsOk ? StringResult.Success($"{setting} set to {Read(setting)}") : saved;
		}
	}
}
