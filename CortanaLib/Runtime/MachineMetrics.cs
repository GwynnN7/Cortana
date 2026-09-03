using System.Globalization;
using CortanaLib.Contracts;

namespace CortanaLib.Runtime;

/// Reads CPU/RAM/GPU/disk/uptime straight from /proc and /sys. Works on both the Pi and the desktop
public static class MachineMetrics
{
	private static readonly string[] CpuHwmon = ["k10temp", "coretemp", "zenpower", "cpu_thermal", "acpitz"];
	private static readonly string[] GpuHwmon = ["amdgpu", "nouveau", "i915"];

	private static (ulong Idle, ulong Total) _previous;

	public static MachineSample Collect()
	{
		(double used, double total) memory = Memory();
		(double used, double total) disk = Disk();

		return new MachineSample(
			Environment.MachineName,
			OperatingSystemName(),
			CpuLoad(),
			Temperature(CpuHwmon),
			memory.used,
			memory.total,
			GpuBusy(),
			Temperature(GpuHwmon),
			disk.used,
			disk.total,
			Uptime(),
			GpuWatts());
	}

	public static string Render(MachineSample sample)
	{
		var lines = new List<string>
		{
			$"{sample.Host} ({sample.Os})",
			$"CPU: {sample.CpuLoad:F0}%{(sample.CpuTemp > 0 ? $" - {sample.CpuTemp:F0}°C" : "")}",
			$"RAM: {sample.MemoryUsed:F1}/{sample.MemoryTotal:F1} GB"
		};

		if (sample.GpuTemp > 0 || sample.GpuLoad > 0)
			lines.Add($"GPU: {sample.GpuLoad:F0}%{(sample.GpuTemp > 0 ? $" - {sample.GpuTemp:F0}°C" : "")}{(sample.GpuPower > 0 ? $" - {sample.GpuPower:F0}W" : "")}");

		lines.Add($"Disk: {sample.DiskUsed:F0}/{sample.DiskTotal:F0} GB");
		lines.Add($"Uptime: {Units.Elapsed(TimeSpan.FromSeconds(sample.Uptime))}");

		return string.Join("\n", lines);
	}

	private static string OperatingSystemName()
	{
		try
		{
			foreach (string line in File.ReadLines("/etc/os-release"))
				if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
					return line[12..].Trim('"');
		}
		catch (IOException) { }

		return Environment.OSVersion.Platform.ToString();
	}

	private static double CpuLoad()
	{
		try
		{
			string[] fields = File.ReadLines("/proc/stat").First().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			ulong[] values = fields.Skip(1).Select(field => ulong.Parse(field, CultureInfo.InvariantCulture)).ToArray();

			ulong idle = values[3] + (values.Length > 4 ? values[4] : 0);
			ulong total = values.Aggregate(0UL, (sum, value) => sum + value);

			(ulong idle, ulong total) previous = _previous;
			_previous = (idle, total);

			if (previous.total == 0 || total <= previous.total) return 0;

			double busy = total - previous.total - (double)(idle - previous.idle);
			return Math.Clamp(busy / (total - previous.total) * 100, 0, 100);
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static (double Used, double Total) Memory()
	{
		try
		{
			var values = new Dictionary<string, double>();
			foreach (string line in File.ReadLines("/proc/meminfo"))
			{
				string[] parts = line.Split(':', 2);
				if (parts.Length != 2) continue;

				string number = parts[1].Trim().Split(' ')[0];
				if (double.TryParse(number, CultureInfo.InvariantCulture, out double kilobytes))
					values[parts[0]] = kilobytes / 1024 / 1024;
			}

			double total = values.GetValueOrDefault("MemTotal");
			double available = values.GetValueOrDefault("MemAvailable");
			return (Math.Max(0, total - available), total);
		}
		catch (Exception)
		{
			return (0, 0);
		}
	}

	private static (double Used, double Total) Disk()
	{
		try
		{
			var root = new DriveInfo("/");
			double total = root.TotalSize / 1024d / 1024 / 1024;
			return (total - root.AvailableFreeSpace / 1024d / 1024 / 1024, total);
		}
		catch (Exception)
		{
			return (0, 0);
		}
	}

	private static long Uptime()
	{
		try
		{
			return (long)double.Parse(File.ReadAllText("/proc/uptime").Split(' ')[0], CultureInfo.InvariantCulture);
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static double Temperature(IEnumerable<string> drivers)
	{
		string[] wanted = drivers as string[] ?? drivers.ToArray();

		try
		{
			foreach (string directory in Directory.EnumerateDirectories("/sys/class/hwmon"))
			{
				string namePath = Path.Combine(directory, "name");
				if (!File.Exists(namePath)) continue;
				if (!wanted.Contains(File.ReadAllText(namePath).Trim())) continue;

				double reading = HighestTemperature(directory);
				if (reading > 0) return reading;
			}
		}
		catch (Exception) { }

		return 0;
	}

	private static double HighestTemperature(string directory)
	{
		double highest = 0;

		foreach (string file in Directory.EnumerateFiles(directory, "temp*_input"))
		{
			try
			{
				if (double.TryParse(File.ReadAllText(file).Trim(), CultureInfo.InvariantCulture, out double milli))
					highest = Math.Max(highest, milli / 1000);
			}
			catch (IOException) { }
		}

		return highest;
	}

	private static double GpuWatts()
	{
		try
		{
			foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
			{
				string hwmon = Path.Combine(card, "device", "hwmon");
				if (!Directory.Exists(hwmon)) continue;

				string sensors = Directory.EnumerateDirectories(hwmon, "hwmon*").FirstOrDefault() ?? "";
				if (sensors.Length > 0 && ReadNumber(Path.Combine(sensors, "power1_average")) is { } microwatts)
					return Math.Round(microwatts / 1_000_000, 1);
			}
		}
		catch (Exception) { }

		return 0;
	}

	private static double GpuBusy()
	{
		double sum = 0;
		var count = 0;

		try
		{
			foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
			{
				string device = Path.Combine(card, "device");
				if (ReadNumber(Path.Combine(device, "gpu_busy_percent")) is not { } busy) continue;

				double effective = busy;
				string hwmon = Path.Combine(device, "hwmon");
				string sensors = Directory.Exists(hwmon)
					? Directory.EnumerateDirectories(hwmon, "hwmon*").FirstOrDefault() ?? ""
					: "";

				if (sensors.Length > 0 &&
					ReadNumber(Path.Combine(sensors, "power1_average")) is { } drawn &&
					ReadNumber(Path.Combine(sensors, "power1_cap")) is { } budget &&
					budget > 0)
					effective = Math.Min(busy * (drawn / budget), 100);

				sum += effective;
				count++;
			}
		}
		catch (Exception) { }

		return count > 0 ? sum / count : 0;
	}

	private static double? ReadNumber(string path)
	{
		try
		{
			return File.Exists(path) && double.TryParse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture, out double value)
				? value
				: null;
		}
		catch (IOException)
		{
			return null;
		}
	}
}
