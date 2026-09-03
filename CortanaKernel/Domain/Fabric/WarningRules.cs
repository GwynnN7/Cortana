using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Fabric;

public interface IWarningRepository
{
	IReadOnlyList<Warning> Load();
	void Save(IReadOnlyList<Warning> warnings);
}

public sealed class WarningStore(IWarningRepository repository)
{
	private readonly Lock _gate = new();
	private readonly List<Warning> _warnings = [.. repository.Load()];

	public IReadOnlyList<Warning> All()
	{
		lock (_gate) return [.. _warnings];
	}

	public void Seed(IReadOnlyList<Warning> defaults)
	{
		lock (_gate)
		{
			if (_warnings.Count > 0) return;

			_warnings.AddRange(defaults);
			repository.Save(_warnings);
		}
	}

	public Result<Warning> Restore(string id, IReadOnlyList<Warning> defaults)
	{
		if (defaults.FirstOrDefault(warning => warning.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is not { } shipped)
			return Result.Fail<Warning>($"'{id}' is not one of the shipped warnings");

		lock (_gate)
		{
			bool enabled = _warnings.FirstOrDefault(warning => warning.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Enabled ?? true;

			_warnings.RemoveAll(warning => warning.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			_warnings.Add(shipped with { Enabled = enabled });

			repository.Save(_warnings);
			return Result.Ok(shipped);
		}
	}

	public IReadOnlyList<string> Adrift(IReadOnlyList<Warning> defaults)
	{
		lock (_gate)
			return
			[
				.. defaults
					.Where(shipped => _warnings.FirstOrDefault(warning => warning.Id.Equals(shipped.Id, StringComparison.OrdinalIgnoreCase))
						is not { } stored || !stored.Triggers.SequenceEqual(shipped.Triggers))
					.Select(shipped => shipped.Id)
			];
	}

	public Result<Warning> Save(Warning warning)
	{
		if (warning.Id.Length == 0) return Result.Fail<Warning>("A warning needs an id");
		if (warning.Triggers.Count == 0) return Result.Fail<Warning>("A warning needs at least one trigger");

		lock (_gate)
		{
			_warnings.RemoveAll(entry => entry.Id.Equals(warning.Id, StringComparison.OrdinalIgnoreCase));
			_warnings.Add(warning);

			repository.Save(_warnings);
			return Result.Ok(warning);
		}
	}

	public IReadOnlyList<string> Purge(string sensor)
	{
		var touched = new List<string>();

		lock (_gate)
		{
			foreach (Warning warning in _warnings.ToList())
			{
				Trigger[] kept = [.. warning.Triggers.Where(trigger => !trigger.Sensor.Equals(sensor, StringComparison.OrdinalIgnoreCase))];
				if (kept.Length == warning.Triggers.Count) continue;

				_warnings.Remove(warning);

				if (kept.Length == 0)
				{
					touched.Add($"{warning.Id} removed, it had no other trigger");
					continue;
				}

				_warnings.Add(warning with { Triggers = kept });
				touched.Add($"{warning.Id} lost its {sensor} trigger");
			}

			if (touched.Count > 0) repository.Save(_warnings);
		}

		return touched;
	}

	public Result<string> Delete(string id)
	{
		lock (_gate)
		{
			if (_warnings.RemoveAll(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) == 0)
				return Result.Fail<string>($"No warning with id '{id}'");

			repository.Save(_warnings);
			return Result.Ok("Deleted");
		}
	}
}

public static class WarningRules
{
	public const double FireMargin = 1.15;
	public const double ClearMargin = 0.9;

	/// Every sustaining trigger has to hold, and then any one of the rest has to be breached
	public static bool Fires(Warning warning, Func<string, Reading?> read)
	{
		Trigger[] gates = [.. warning.Triggers.Where(trigger => trigger.Sustains)];
		Trigger[] raisers = [.. warning.Triggers.Where(trigger => !trigger.Sustains)];

		if (gates.Any(trigger => !Breached(trigger, read(trigger.Sensor), FireMargin))) return false;

		return raisers.Length > 0
			? raisers.Any(trigger => Breached(trigger, read(trigger.Sensor), FireMargin))
			: gates.Length > 0;
	}

	public static bool Clears(Warning warning, Func<string, Reading?> read)
	{
		Trigger[] gates = [.. warning.Triggers.Where(trigger => trigger.Sustains)];
		if (gates.Any(trigger => !Breached(trigger, read(trigger.Sensor), ClearMargin))) return true;

		return warning.Triggers.Where(trigger => !trigger.Sustains)
			.All(trigger => !Breached(trigger, read(trigger.Sensor), ClearMargin));
	}

	public static string Explain(Warning warning, Func<string, Reading?> read, Func<string, VirtualSensor?> describe)
	{
		string[] parts =
		[
			.. warning.Triggers.Select(trigger =>
			{
				VirtualSensor? sensor = describe(trigger.Sensor);
				string name = sensor?.Name ?? trigger.Sensor;
				string unit = sensor?.Unit ?? "";
				string value = read(trigger.Sensor) is { } reading ? Units.Number(reading.Value) : "no reading";

				if (trigger.Kind is TriggerKind.IsTrue or TriggerKind.IsFalse)
					return $"{name} is {(read(trigger.Sensor) is { Value: >= 0.5 } ? "true" : "false")}";

				double limit = trigger.High ?? trigger.Low ?? 0;
				return $"{name} {value}{unit} against {Units.Number(limit)}{unit}";
			})
		];

		return string.Join(", ", parts);
	}

	/// The margin only makes sense for a threshold. A boolean is simply true or it is not
	private static bool Breached(Trigger trigger, Reading? reading, double margin)
	{
		if (reading is null) return false;

		return trigger.Kind switch
		{
			TriggerKind.IsTrue => reading.Value >= 0.5,
			TriggerKind.IsFalse => reading.Value < 0.5,
			TriggerKind.Above => trigger.High is { } high && reading.Value >= high * margin,
			TriggerKind.Below => trigger.Low is { } low && reading.Value <= low * (2 - margin),
			TriggerKind.Outside => (trigger.Low is { } min && reading.Value <= min * (2 - margin))
				|| (trigger.High is { } max && reading.Value >= max * margin),
			_ => false
		};
	}
}

public sealed class WarningState
{
	private readonly Lock _gate = new();
	private readonly Dictionary<string, DateTimeOffset> _active = new(StringComparer.OrdinalIgnoreCase);

	public bool Any
	{
		get { lock (_gate) return _active.Count > 0; }
	}

	public bool IsActive(string warning)
	{
		lock (_gate) return _active.ContainsKey(warning);
	}

	public DateTimeOffset? Since(string warning)
	{
		lock (_gate) return _active.TryGetValue(warning, out DateTimeOffset at) ? at : null;
	}

	public void Raise(string warning, DateTimeOffset at)
	{
		lock (_gate) _active[warning] = at;
	}

	public void Clear(string warning)
	{
		lock (_gate) _active.Remove(warning);
	}
}
