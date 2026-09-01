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

	private static readonly string[] Columns =
	[
		"temperature", "humidity", "light", "co2", "tvoc", "motion", "lamp", "computer",
		"pi_cpu", "pi_temp", "pi_ram", "pc_cpu", "pc_temp", "pc_ram", "pc_gpu", "pc_gpu_temp"
	];

	private static readonly string Header = "timestamp," + string.Join(",", Columns);

	private readonly Lock _gate = new();

	public IReadOnlyList<string> Metrics => Columns;

	public void Append(HistorySample sample)
	{
		string row = string.Join(",", new[] { sample.At.ToString("s", CultureInfo.InvariantCulture) }
			.Concat(Columns.Select(column => sample.Values.GetValueOrDefault(column) is { } value
				? Math.Round(value, 1).ToString(CultureInfo.InvariantCulture)
				: "")));

		string path = PathFor(DateOnly.FromDateTime(sample.At.LocalDateTime));

		lock (_gate)
		{
			Directory.CreateDirectory(Folder);
			bool fresh = !File.Exists(path);

			using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
			if (fresh) writer.WriteLine(Header);
			writer.WriteLine(row);
		}
	}

	public IReadOnlyList<HistoryPoint> Read(string metric, DateTimeOffset from, DateTimeOffset to)
	{
		var points = new List<HistoryPoint>();
		string wanted = metric.ToLowerInvariant();
		if (!Columns.Contains(wanted)) return points;

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

					int column = Array.IndexOf(lines[0].TrimStart('﻿').Split(','), wanted);
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
