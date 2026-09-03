using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Domain.Fabric;

public interface IBindRepository
{
	IReadOnlyList<Bind> Load();
	void Save(IReadOnlyList<Bind> binds);
}

public sealed class BindStore(IBindRepository repository)
{
	private readonly Lock _gate = new();
	private readonly List<Bind> _binds = [.. repository.Load()];

	public IReadOnlyList<Bind> All()
	{
		lock (_gate) return [.. _binds];
	}

	public IReadOnlyList<Bind> For(string device)
	{
		lock (_gate) return [.. _binds.Where(bind => bind.Device.Equals(device, StringComparison.OrdinalIgnoreCase))];
	}

	public bool Holds(string device)
	{
		lock (_gate)
			return _binds.Any(bind => bind.Enabled && bind.HoldsOnManualAction
				&& bind.Device.Equals(device, StringComparison.OrdinalIgnoreCase));
	}

	/// Shipped contents are never migrated onto a live install, so restoring one is an explicit act
	public Result<Bind> Restore(string id, IReadOnlyList<Bind> defaults)
	{
		if (defaults.FirstOrDefault(bind => bind.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is not { } shipped)
			return Result.Fail<Bind>($"'{id}' is not one of the shipped binds");

		lock (_gate)
		{
			bool enabled = _binds.FirstOrDefault(bind => bind.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Enabled ?? true;

			_binds.RemoveAll(bind => bind.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
			_binds.Add(shipped with { Enabled = enabled });

			repository.Save(_binds);
			return Result.Ok(shipped);
		}
	}

	/// Which shipped binds are missing or differ from what is stored
	public IReadOnlyList<string> Adrift(IReadOnlyList<Bind> defaults)
	{
		lock (_gate)
			return
			[
				.. defaults
					.Where(shipped => _binds.FirstOrDefault(bind => bind.Id.Equals(shipped.Id, StringComparison.OrdinalIgnoreCase))
						is not { } stored || !stored.Triggers.SequenceEqual(shipped.Triggers))
					.Select(shipped => shipped.Id)
			];
	}

	public Result<Bind> Save(Bind bind)
	{
		if (bind.Device.Length == 0) return Result.Fail<Bind>("A bind needs a device");
		if (bind.Triggers.Count == 0) return Result.Fail<Bind>("A bind needs at least one trigger");

		lock (_gate)
		{
			int existing = _binds.FindIndex(entry => entry.Id.Equals(bind.Id, StringComparison.OrdinalIgnoreCase));

			if (existing >= 0) _binds[existing] = bind;
			else _binds.Add(bind);

			repository.Save(_binds);
			return Result.Ok(bind);
		}
	}

	public IReadOnlyList<string> Purge(string device, string sensor)
	{
		var touched = new List<string>();

		lock (_gate)
		{
			foreach (Bind bind in _binds.ToList())
			{
				if (bind.Device.Equals(device, StringComparison.OrdinalIgnoreCase))
				{
					_binds.Remove(bind);
					touched.Add($"{bind.Id} removed with its device");
					continue;
				}

				Trigger[] kept = [.. bind.Triggers.Where(trigger => !trigger.Sensor.Equals(sensor, StringComparison.OrdinalIgnoreCase))];
				if (kept.Length == bind.Triggers.Count) continue;

				_binds.Remove(bind);

				if (kept.Length == 0)
				{
					touched.Add($"{bind.Id} removed, it had no other trigger");
					continue;
				}

				_binds.Add(bind with { Triggers = kept });
				touched.Add($"{bind.Id} lost its {sensor} trigger");
			}

			if (touched.Count > 0) repository.Save(_binds);
		}

		return touched;
	}

	public Result<string> Delete(string id)
	{
		lock (_gate)
		{
			if (_binds.RemoveAll(bind => bind.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) == 0)
				return Result.Fail<string>($"No bind with id '{id}'");

			repository.Save(_binds);
			return Result.Ok("Deleted");
		}
	}
}
