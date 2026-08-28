using System.Diagnostics;
using System.Globalization;
using CortanaLib.Structures;

namespace CortanaDesktop;

internal static class SystemMonitor
{
	private static readonly string[] CpuHwmon = ["k10temp", "coretemp", "zenpower", "cpu_thermal", "acpitz"];
	private static readonly string[] GpuHwmon = ["amdgpu", "nouveau", "i915"];

	private static (ulong Idle, ulong Total) _previous;

	public static PostMetrics Collect()
	{
		(double used, double total) memory = Memory();
		(double used, double total) disk = Disk();
		(double load, double temp) gpu = Gpu();

		return new PostMetrics(
			Environment.MachineName,
			Os(),
			CpuLoad(),
			Temperature(CpuHwmon),
			memory.used,
			memory.total,
			gpu.load,
			gpu.temp,
			disk.used,
			disk.total,
			Uptime());
	}

	private static string Os()
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

			double busy = (total - previous.total) - (double)(idle - previous.idle);
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
			string seconds = File.ReadAllText("/proc/uptime").Split(' ')[0];
			return (long)double.Parse(seconds, CultureInfo.InvariantCulture);
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static double Temperature(IEnumerable<string> drivers)
	{
		try
		{
			foreach (string directory in Directory.EnumerateDirectories("/sys/class/hwmon"))
			{
				string namePath = Path.Combine(directory, "name");
				if (!File.Exists(namePath)) continue;

				string name = File.ReadAllText(namePath).Trim();
				if (!drivers.Contains(name)) continue;

				double reading = HighestTemp(directory);
				if (reading > 0) return reading;
			}
		}
		catch (Exception) { }

		return 0;
	}

	private static double HighestTemp(string directory)
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

	private static (double Load, double Temp) Gpu()
	{
		double amdTemp = Temperature(GpuHwmon);
		return (AmdBusy(), amdTemp);
	}

	private static double AmdBusy()
	{
		try
		{
			foreach (string card in Directory.EnumerateDirectories("/sys/class/drm", "card?"))
			{
				string path = Path.Combine(card, "device", "gpu_busy_percent");
				if (File.Exists(path) && double.TryParse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture, out double busy))
					return busy;
			}
		}
		catch (Exception) { }

		return 0;
	}
}
