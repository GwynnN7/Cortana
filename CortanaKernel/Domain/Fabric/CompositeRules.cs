using CortanaLib.Contracts;

namespace CortanaKernel.Domain.Fabric;

/// What a composite is worth, and the little state that makes it a level rather than an edge
public readonly record struct CompositeState(bool High, DateTimeOffset? OpenedAt, bool Opening = false);

public static class CompositeRules
{
	/// On when something allowed to start it says so, for a while after that, and then for as long as
	/// anything at all still holds it up. The middle term is what turns a PIR — high for a moment and
	/// then gone — into something a lamp can be bound to. The third is why a machine woken from
	/// somewhere else keeps a presence that was already there and never announces one
	public static CompositeState Evaluate(Composite composite, CompositeState was, TimeSpan hold,
		DateTimeOffset now, Func<string, Reading?> read)
	{
		bool opens = Satisfied(composite.Inputs.Where(input => input.Starts), composite.Mode, read);
		bool holds = Satisfied(composite.Inputs, composite.Mode, read);

		DateTimeOffset? openedAt = opens ? now : was.OpenedAt;
		bool within = openedAt is { } since && now - since < hold;

		return new CompositeState(opens || within || (was.High && holds), openedAt, opens);
	}

	private static bool Satisfied(IEnumerable<CompositeInput> inputs, BindMode mode, Func<string, Reading?> read)
	{
		CompositeInput[] all = [.. inputs];
		if (all.Length == 0) return false;

		return mode == BindMode.All
			? all.All(input => Holds(input, read(input.Sensor)))
			: all.Any(input => Holds(input, read(input.Sensor)));
	}

	private static bool Holds(CompositeInput input, Reading? reading) =>
		BindRules.Holds(new Trigger(input.Sensor, input.Kind, input.Low, input.High), reading);

	/// A composite may read another one, so they are worked out feeders first. A cycle cannot be
	/// ordered at all: it is refused at registration, and if one ever gets in it simply reads what the
	/// last pass left rather than looping
	public static IReadOnlyList<Composite> InOrder(IReadOnlyList<Composite> composites)
	{
		Dictionary<string, Composite> byId = composites
			.GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

		var ordered = new List<Composite>();
		var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var walking = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void Place(Composite composite)
		{
			if (placed.Contains(composite.Id) || !walking.Add(composite.Id)) return;

			foreach (CompositeInput input in composite.Inputs)
				if (byId.TryGetValue(input.Sensor, out Composite? feeder))
					Place(feeder);

			walking.Remove(composite.Id);
			if (placed.Add(composite.Id)) ordered.Add(composite);
		}

		foreach (Composite composite in composites) Place(composite);
		return ordered;
	}

	/// Whether adding this one would make a composite depend on itself, however far around
	public static bool Cycles(Composite candidate, IReadOnlyList<Composite> existing)
	{
		Dictionary<string, Composite> byId = existing
			.Where(entry => !entry.Id.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase))
			.GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
			.ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

		byId[candidate.Id] = candidate;

		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		bool Reaches(string from)
		{
			if (!seen.Add(from)) return false;
			if (!byId.TryGetValue(from, out Composite? composite)) return false;

			foreach (CompositeInput input in composite.Inputs)
			{
				if (input.Sensor.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase)) return true;
				if (Reaches(input.Sensor)) return true;
			}

			return false;
		}

		return candidate.Inputs.Any(input => input.Sensor.Equals(candidate.Id, StringComparison.OrdinalIgnoreCase))
			|| candidate.Inputs.Any(input => Reaches(input.Sensor));
	}
}
