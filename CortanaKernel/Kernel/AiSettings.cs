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
			[EAiSetting.DiscordMinutes] = (0.5, 120),
			[EAiSetting.HistoryMinutes] = (1, 60),
			[EAiSetting.HistoryDays] = (1, 3650),
			[EAiSetting.NotifySeconds] = (1, 120)
		};

	private sealed class Stored
	{
		public string Model { get; set; } = nameof(ELlmModel.FlashLite);
		public double Temperature { get; set; } = 0.9;
		public int History { get; set; } = 8;
		public double DiscordMinutes { get; set; } = 1;
		public int HistoryMinutes { get; set; } = 5;
		public int HistoryDays { get; set; } = 180;
		public double NotifySeconds { get; set; } = 5;
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
	public static int HistoryMinutes { get { lock (Gate) return _values.HistoryMinutes; } }
	public static int HistoryDays { get { lock (Gate) return _values.HistoryDays; } }
	public static double NotifySeconds { get { lock (Gate) return _values.NotifySeconds; } }

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

		return new Stored();
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
		EAiSetting.HistoryMinutes => HistoryMinutes.ToString(CultureInfo.InvariantCulture),
		EAiSetting.HistoryDays => HistoryDays.ToString(CultureInfo.InvariantCulture),
		EAiSetting.NotifySeconds => NotifySeconds.ToString("0.##", CultureInfo.InvariantCulture),
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
				case EAiSetting.HistoryMinutes: _values.HistoryMinutes = (int)value; break;
				case EAiSetting.HistoryDays: _values.HistoryDays = (int)value; break;
				case EAiSetting.NotifySeconds: _values.NotifySeconds = value; break;
			}

			StringResult saved = Save();
			if (!saved.IsOk) return saved;

			if (setting == EAiSetting.HistoryMinutes) HistoryService.Reschedule();
			if (setting == EAiSetting.HistoryDays) HistoryService.Prune();

			return StringResult.Success($"{setting} set to {Read(setting)}");
		}
	}
}
