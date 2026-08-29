using System.Globalization;
using System.Text;
using CortanaKernel.Hardware;
using CortanaLib;
using CortanaLib.Structures;

namespace CortanaKernel.Kernel;

public static class HistoryService
{
	private const string Header = "timestamp,temperature,humidity,light,co2,tvoc,motion,lamp,computer,pi_cpu,pi_temp,pi_ram,pc_cpu,pc_temp,pc_ram,pc_gpu,pc_gpu_temp";

	private static readonly string Folder = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaKernel)}/History");
	private static readonly Lock Gate = new();

	private static System.Threading.Timer? _timer;

	public static void Start()
	{
		Schedule();
		Prune();
	}

	private static void Schedule()
	{
		TimeSpan every = TimeSpan.FromMinutes(Math.Clamp(AiSettings.HistoryMinutes, 1, 60));

		_timer?.Dispose();
		_timer = new System.Threading.Timer(_ => Sample(), null, every, every);
	}

	public static void Reschedule() => Schedule();

	private static string PathFor(DateOnly day) => Path.Combine(Folder, $"{day:yyyy-MM-dd}.csv");

	private static void Sample()
	{
		try
		{
			string row = BuildRow();
			string path = PathFor(DateOnly.FromDateTime(DateTime.Now));

			lock (Gate)
			{
				Directory.CreateDirectory(Folder);
				bool fresh = !File.Exists(path);

				using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
				if (fresh) writer.WriteLine(Header);
				writer.WriteLine(row);
			}

			if (DateTime.Now.Hour == 0 && DateTime.Now.Minute < 10) Prune();
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[History] Could not record a sample: {ex.Message}");
		}
	}

	private static string BuildRow()
	{
		Dictionary<string, string> sensors = HardwareApi.Sensors.GetAllData()
			.ToDictionary(sensor => sensor.Sensor, sensor => sensor.Value);

		Dictionary<string, string> devices = HardwareApi.Devices.GetAllPower()
			.ToDictionary(device => device.Device, device => device.Status);

		MetricsResponse pi = MetricsStore.Local();
		MetricsResponse? pc = MetricsStore.Latest().Match<MetricsResponse?>(metrics => metrics.Stale ? null : metrics, () => null);

		var cells = new List<string>
		{
			DateTime.Now.ToString("s", CultureInfo.InvariantCulture),
			Number(sensors.GetValueOrDefault(nameof(ESensor.Temperature))),
			Number(sensors.GetValueOrDefault(nameof(ESensor.Humidity))),
			Number(sensors.GetValueOrDefault(nameof(ESensor.Light))),
			Number(sensors.GetValueOrDefault(nameof(ESensor.CO2))),
			Number(sensors.GetValueOrDefault(nameof(ESensor.Tvoc))),
			Flag(sensors.GetValueOrDefault(nameof(ESensor.Motion))),
			Flag(devices.GetValueOrDefault(nameof(EDevice.Lamp))),
			Flag(devices.GetValueOrDefault(nameof(EDevice.Computer))),
			Round(pi.CpuLoad),
			Round(pi.CpuTemp),
			Round(pi.MemoryTotal > 0 ? pi.MemoryUsed / pi.MemoryTotal * 100 : 0),
			pc == null ? "" : Round(pc.CpuLoad),
			pc == null ? "" : Round(pc.CpuTemp),
			pc == null ? "" : Round(pc.MemoryTotal > 0 ? pc.MemoryUsed / pc.MemoryTotal * 100 : 0),
			pc == null ? "" : Round(pc.GpuLoad),
			pc == null ? "" : Round(pc.GpuTemp)
		};

		return string.Join(",", cells);
	}

	private static string Number(string? value)
	{
		if (string.IsNullOrWhiteSpace(value)) return "";

		string cleaned = new(value.Where(character => char.IsDigit(character) || character is '.' or '-').ToArray());
		return double.TryParse(cleaned, CultureInfo.InvariantCulture, out double parsed) ? Round(parsed) : "";
	}

	private static string Flag(string? value) => value switch
	{
		null => "",
		_ when value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals(nameof(EStatus.On), StringComparison.OrdinalIgnoreCase) => "1",
		_ => "0"
	};

	private static string Round(double value) => Math.Round(value, 1).ToString(CultureInfo.InvariantCulture);

	public static void Prune()
	{
		try
		{
			if (!Directory.Exists(Folder)) return;

			DateOnly cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-Math.Clamp(AiSettings.HistoryDays, 1, 3650));

			foreach (string file in Directory.EnumerateFiles(Folder, "*.csv"))
			{
				if (!DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", out DateOnly day)) continue;
				if (day < cutoff) File.Delete(file);
			}
		}
		catch (Exception ex)
		{
			DataHandler.Log($"[History] Could not prune: {ex.Message}");
		}
	}

	public static IReadOnlyList<HistoryPoint> Read(string metric, DateTime from, DateTime to)
	{
		var points = new List<HistoryPoint>();
		string wanted = metric.ToLowerInvariant();
		if (!Metrics.Contains(wanted)) return points;

		for (DateOnly day = DateOnly.FromDateTime(from); day <= DateOnly.FromDateTime(to); day = day.AddDays(1))
		{
			string path = PathFor(day);
			if (!File.Exists(path)) continue;

			try
			{
				lock (Gate)
				{
					string[] lines = File.ReadAllLines(path);
					if (lines.Length < 2) continue;

					int column = Array.IndexOf(lines[0].TrimStart('﻿').Split(','), wanted);
					if (column < 1) continue;

					foreach (string line in lines.Skip(1))
					{
						string[] cells = line.Split(',');
						if (cells.Length <= column) continue;
						if (!DateTime.TryParse(cells[0], CultureInfo.InvariantCulture, out DateTime stamp)) continue;
						if (stamp < from || stamp > to) continue;
						if (!double.TryParse(cells[column], CultureInfo.InvariantCulture, out double value)) continue;

						points.Add(new HistoryPoint(stamp, value));
					}
				}
			}
			catch (IOException)
			{
			}
		}

		return points;
	}

	public static IReadOnlyList<string> Metrics => Header.Split(',').Skip(1).ToList();

	public static long DiskUsage()
	{
		try
		{
			return Directory.Exists(Folder)
				? Directory.EnumerateFiles(Folder, "*.csv").Sum(file => new FileInfo(file).Length)
				: 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}
}
