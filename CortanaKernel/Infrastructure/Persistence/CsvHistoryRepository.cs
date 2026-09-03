using System.Globalization;
using System.Text;
using CortanaKernel.Domain.History;
using CortanaLib.Contracts;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Persistence;

/// One CSV per day. Small, greppable and fast enough for a few months of samples
public sealed class CsvHistoryRepository : IHistoryRepository
{
	private static readonly string Folder = KernelFiles.Path("History");

	private readonly Lock _gate = new();

	/// Whatever the files hold, so a metric survives the sensor that produced it being unregistered
	public IReadOnlyList<string> Metrics
	{
		get
		{
			lock (_gate)
			{
				var known = new List<string>();

				foreach (string file in Files())
					foreach (string column in Columns(file))
						if (!known.Contains(column, StringComparer.OrdinalIgnoreCase)) known.Add(column);

				return known;
			}
		}
	}

	public void Append(HistorySample sample)
	{
		string path = PathFor(DateOnly.FromDateTime(sample.At.LocalDateTime));
		string[] wanted = [.. sample.Values.Keys];

		lock (_gate)
		{
			Directory.CreateDirectory(Folder);

			string[] columns = File.Exists(path) ? Columns(path) : [];
			string[] missing = [.. wanted.Where(column => !columns.Contains(column, StringComparer.OrdinalIgnoreCase))];

			if (missing.Length > 0)
			{
				columns = [.. columns, .. missing];
				Widen(path, columns);
			}

			using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
			writer.WriteLine(string.Join(",", new[] { sample.At.ToString("s", CultureInfo.InvariantCulture) }
				.Concat(columns.Select(column => sample.Values.GetValueOrDefault(column) is { } value
					? Math.Round(value, 1).ToString(CultureInfo.InvariantCulture)
					: ""))));
		}
	}

	/// A day already on disk keeps its rows, they just gain empty cells for the new columns
	private static void Widen(string path, string[] columns)
	{
		string header = "timestamp," + string.Join(",", columns);

		if (!File.Exists(path))
		{
			File.WriteAllText(path, header + Environment.NewLine, Encoding.UTF8);
			return;
		}

		string[] lines = File.ReadAllLines(path);
		var rebuilt = new List<string> { header };

		foreach (string line in lines.Skip(1))
		{
			if (line.Length == 0) continue;

			int cells = line.Split(',').Length;
			rebuilt.Add(line + new string(',', Math.Max(0, columns.Length + 1 - cells)));
		}

		File.WriteAllLines(path, rebuilt, Encoding.UTF8);
	}

	private static IEnumerable<string> Files() =>
		Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "*.csv").OrderByDescending(file => file) : [];

	private static string[] Columns(string path)
	{
		try
		{
			using var reader = new StreamReader(path, Encoding.UTF8);
			return reader.ReadLine() is { } header ? [.. header.TrimStart('\ufeff').Split(',').Skip(1)] : [];
		}
		catch (IOException)
		{
			return [];
		}
	}

	public IReadOnlyList<HistoryPoint> Read(string metric, DateTimeOffset from, DateTimeOffset to)
	{
		var points = new List<HistoryPoint>();
		string wanted = metric.Trim();

		for (DateOnly day = DateOnly.FromDateTime(from.LocalDateTime); day <= DateOnly.FromDateTime(to.LocalDateTime); day = day.AddDays(1))
		{
			string path = PathFor(day);
			if (!File.Exists(path)) continue;

			try
			{
				lock (_gate)
				{
					string[] lines = File.ReadAllLines(path);
					if (lines.Length < 2) continue;

					int column = Array.FindIndex(lines[0].TrimStart('﻿').Split(','),
						name => name.Equals(wanted, StringComparison.OrdinalIgnoreCase));
					if (column < 1) continue;

					foreach (string line in lines.Skip(1))
					{
						string[] cells = line.Split(',');
						if (cells.Length <= column) continue;
						if (!DateTimeOffset.TryParse(cells[0], CultureInfo.InvariantCulture, out DateTimeOffset at)) continue;
						if (at < from || at > to) continue;
						if (!double.TryParse(cells[column], CultureInfo.InvariantCulture, out double value)) continue;

						points.Add(new HistoryPoint(at, value));
					}
				}
			}
			catch (IOException) { }
		}

		return points;
	}

	public void Prune(int retentionDays)
	{
		try
		{
			if (!Directory.Exists(Folder)) return;

			DateOnly cutoff = DateOnly.FromDateTime(DateTime.Now).AddDays(-Math.Clamp(retentionDays, 1, 3650));

			foreach (string file in Directory.EnumerateFiles(Folder, "*.csv"))
			{
				if (!DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", out DateOnly day)) continue;
				if (day < cutoff) File.Delete(file);
			}
		}
		catch (Exception ex)
		{
			Log.Error("History", $"Could not prune: {ex.Message}");
		}
	}

	public long DiskUsage()
	{
		try
		{
			return Directory.Exists(Folder) ? Directory.EnumerateFiles(Folder, "*.csv").Sum(file => new FileInfo(file).Length) : 0;
		}
		catch (Exception)
		{
			return 0;
		}
	}

	private static string PathFor(DateOnly day) => Path.Combine(Folder, $"{day:yyyy-MM-dd}.csv");
}
