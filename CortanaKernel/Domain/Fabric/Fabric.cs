using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Fabric;

public interface IFabricRepository
{
	IReadOnlyList<SourceDescriptor> LoadSources();
	void SaveSources(IReadOnlyList<SourceDescriptor> sources);
	Registrations LoadRegistrations();
	void SaveRegistrations(Registrations registrations);
	IReadOnlyDictionary<string, PowerState> LoadChannels();
	void SaveChannels(IReadOnlyDictionary<string, PowerState> channels);
}

public sealed record Reading(double Value, DateTimeOffset At);

public sealed class Fabric(IFabricRepository repository)
{
	private readonly Lock _gate = new();

	private readonly Dictionary<string, SourceDescriptor> _sources =
		repository.LoadSources().ToDictionary(source => source.Id, StringComparer.OrdinalIgnoreCase);

	private readonly List<VirtualDevice> _devices = [.. repository.LoadRegistrations().Devices];
	private readonly List<VirtualSensor> _sensors = [.. repository.LoadRegistrations().Sensors];
	private readonly List<Composite> _composites = [.. repository.LoadRegistrations().Composites];

	private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.OrdinalIgnoreCase);
	/// State belongs to the channel, so two devices sharing an output never disagree about it.
	/// An output cannot be read back, so what was last written is kept and re-asserted on startup
	private readonly Dictionary<string, PowerState> _states =
		new(repository.LoadChannels(), StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, Reading> _readings = new(StringComparer.OrdinalIgnoreCase);

	/// What each source says about itself. Live, not persisted: it arrives with the connection
	private readonly Dictionary<string, List<SourceFact>> _facts = new(StringComparer.OrdinalIgnoreCase);

	private bool _seeded;

	public IReadOnlyList<SourceDescriptor> Sources
	{
		get { lock (_gate) return [.. _sources.Values]; }
	}

	public void Seed(IReadOnlyList<SourceDescriptor> sources, Registrations registrations)
	{
		lock (_gate)
		{
			if (_seeded) return;
			_seeded = true;

			var added = false;

			// Only the built-in kernel source is ours to refresh. Keep hardware announcements across
			// restarts, including a Pi pin map which may already have been loaded by the GPIO writer.
			foreach (SourceDescriptor source in sources)
			{
				if (_sources.TryGetValue(source.Id, out SourceDescriptor? existing)
					&& (!source.Id.Equals(SourceIds.Kernel, StringComparison.OrdinalIgnoreCase)
					|| (existing.Kind == source.Kind
					&& existing.Outputs.SequenceEqual(source.Outputs, StringComparer.OrdinalIgnoreCase)
					&& existing.Inputs.SequenceEqual(source.Inputs, StringComparer.OrdinalIgnoreCase))))
					continue;

				_sources[source.Id] = source;
				added = true;
			}

			if (added) repository.SaveSources([.. _sources.Values]);

			VirtualDevice[] freshDevices =
			[
				.. registrations.Devices.Where(device =>
					!_devices.Any(entry => entry.Id.Equals(device.Id, StringComparison.OrdinalIgnoreCase)))
			];

			VirtualSensor[] freshSensors =
			[
				.. registrations.Sensors.Where(sensor =>
					!_sensors.Any(entry => entry.Id.Equals(sensor.Id, StringComparison.OrdinalIgnoreCase))
					&& !_composites.Any(entry => entry.Id.Equals(sensor.Id, StringComparison.OrdinalIgnoreCase)))
			];

			// Only remove the old built-in derived sensor, never a user's hardware sensor with the
			// same id as a new shipped default.
			int stale = _sensors.RemoveAll(sensor =>
				sensor.Id.Equals(SensorIds.Presence, StringComparison.OrdinalIgnoreCase)
				&& sensor.Source.Equals(SourceIds.Kernel, StringComparison.OrdinalIgnoreCase)
				&& sensor.Channel.Equals(SensorIds.Presence, StringComparison.OrdinalIgnoreCase));

			Composite[] freshComposites =
			[
				.. registrations.Composites.Where(composite =>
					!_composites.Any(entry => entry.Id.Equals(composite.Id, StringComparison.OrdinalIgnoreCase))
					&& !_sensors.Any(entry => entry.Id.Equals(composite.Id, StringComparison.OrdinalIgnoreCase)))
			];

			if (freshDevices.Length > 0 || freshSensors.Length > 0 || freshComposites.Length > 0 || stale > 0)
			{
				_devices.AddRange(freshDevices);
				_sensors.AddRange(freshSensors);
				_composites.AddRange(freshComposites);
				Save();
			}
		}
	}

	public bool Announce(SourceDescriptor source)
	{
		lock (_gate)
		{
			bool changed = !_sources.TryGetValue(source.Id, out SourceDescriptor? existing) || existing != source;

			_sources[source.Id] = source;
			_seen[source.Id] = DateTimeOffset.Now;

			if (changed) repository.SaveSources([.. _sources.Values]);
			return changed;
		}
	}

	public void Touch(string source)
	{
		lock (_gate) _seen[source] = DateTimeOffset.Now;
	}

	/// A source that went away stops having readings, or its last ones go on being believed for ever
	public void Dropped(string source)
	{
		lock (_gate)
		{
			_seen.Remove(source);

			foreach (VirtualSensor sensor in _sensors.Where(sensor => sensor.Source.Equals(source, StringComparison.OrdinalIgnoreCase)))
				_readings.Remove(sensor.Id);
		}
	}

	/// Merges what a source tells us about itself, keeping the order it gave and the keys it did not resend
	public void Describe(string source, IReadOnlyDictionary<string, string>? facts)
	{
		if (facts is not { Count: > 0 }) return;

		lock (_gate)
		{
			List<SourceFact> known = _facts.TryGetValue(source, out List<SourceFact>? stored) ? stored : [];

			foreach ((string key, string value) in facts)
			{
				int at = known.FindIndex(fact => fact.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

				if (at >= 0) known[at] = new SourceFact(key, value);
				else known.Add(new SourceFact(key, value));
			}

			_facts[source] = known;
		}
	}

	public IReadOnlyList<SourceFact> Facts(string source)
	{
		lock (_gate) return _facts.TryGetValue(source, out List<SourceFact>? known) ? [.. known] : [];
	}

	public string? Fact(string source, string key)
	{
		lock (_gate)
			return _facts.TryGetValue(source, out List<SourceFact>? known)
				? known.FirstOrDefault(fact => fact.Key.Equals(key, StringComparison.OrdinalIgnoreCase))?.Value
				: null;
	}

	public bool IsOnline(string source)
	{
		lock (_gate) return _seen.ContainsKey(source);
	}

	// ---------- registrations ----------

	/// A caller may say either the id or the name: a person asks for the speakers, not for 'generic'
	public VirtualDevice? Device(string device)
	{
		lock (_gate)
			return _devices.FirstOrDefault(entry => entry.Id.Equals(device, StringComparison.OrdinalIgnoreCase))
				?? _devices.FirstOrDefault(entry => entry.Name.Equals(device, StringComparison.OrdinalIgnoreCase));
	}

	public VirtualSensor? Sensor(string sensor)
	{
		lock (_gate)
		{
			List<VirtualSensor> all = [.. _sensors, .. _composites.Select(AsSensor)];

			return all.FirstOrDefault(entry => entry.Id.Equals(sensor, StringComparison.OrdinalIgnoreCase))
				?? all.FirstOrDefault(entry => entry.Name.Equals(sensor, StringComparison.OrdinalIgnoreCase));
		}
	}

	/// A composite is a sensor everywhere except where it is defined, so it is drawn, charted, bound
	/// and read exactly like one. Its channel is its own id: nothing announced it, Cortana works it out
	private static VirtualSensor AsSensor(Composite composite) => new(
		composite.Id, composite.Name, SourceIds.Kernel, composite.Id, "", ReadingKind.Boolean,
		composite.IconHigh, composite.IconLow, InStatus: composite.InStatus);

	public IReadOnlyList<Composite> Composites
	{
		get { lock (_gate) return [.. _composites]; }
	}

	public Result<Composite> Register(Composite composite)
	{
		lock (_gate)
		{
			if (string.IsNullOrWhiteSpace(composite.Id) || string.IsNullOrWhiteSpace(composite.Name))
				return Result.Fail<Composite>("A composite needs an id and a name");

			if (composite.Inputs is not { Count: > 0 })
				return Result.Fail<Composite>("A composite with no inputs is never true");

			if (composite.HoldSeconds < 0)
				return Result.Fail<Composite>("Hold seconds cannot be negative");

			if (!Enum.IsDefined(composite.Mode)) return Result.Fail<Composite>("Choose Any or All inputs");
			var inputs = new List<CompositeInput>();
			foreach (CompositeInput input in composite.Inputs)
			{
				if (input is null || string.IsNullOrWhiteSpace(input.Sensor) || Sensor(input.Sensor) is not { } sensor)
					return Result.Fail<Composite>("Every input must name an existing sensor or composite");
				if (!Enum.IsDefined(input.Kind) || input.Kind == TriggerKind.Changed)
					return Result.Fail<Composite>("A composite needs a level condition, not Changed");
				if ((input.Low.HasValue && !double.IsFinite(input.Low.Value))
					|| (input.High.HasValue && !double.IsFinite(input.High.Value))
					|| (input.Kind == TriggerKind.Below && !input.Low.HasValue)
					|| (input.Kind == TriggerKind.Above && !input.High.HasValue)
					|| (input.Kind == TriggerKind.Outside && (!input.Low.HasValue || !input.High.HasValue || input.Low > input.High)))
					return Result.Fail<Composite>("Supply valid thresholds for each input condition");
				inputs.Add(input with { Sensor = sensor.Id });
			}
			composite = composite with { Inputs = inputs };

			if (_sensors.Any(entry => entry.Id.Equals(composite.Id, StringComparison.OrdinalIgnoreCase)))
				return Result.Fail<Composite>($"'{composite.Id}' is already a sensor");

			if (CompositeRules.Cycles(composite, _composites))
				return Result.Fail<Composite>($"'{composite.Id}' would end up reading itself");

			_composites.RemoveAll(entry => entry.Id.Equals(composite.Id, StringComparison.OrdinalIgnoreCase));
			_composites.Add(composite);

			Save();
			return Result.Ok(composite);
		}
	}

	/// Written by the engine rather than by a source, because nothing out there measures it
	public bool Compute(string composite, double value, DateTimeOffset at)
	{
		lock (_gate)
		{
			if (!_composites.Any(entry => entry.Id.Equals(composite, StringComparison.OrdinalIgnoreCase))) return false;
			bool moved = !_readings.TryGetValue(composite, out Reading? previous)
				|| Math.Abs(previous.Value - value) >= double.Epsilon;

			_readings[composite] = moved ? new Reading(value, at) : previous! with { At = at };
			return moved;
		}
	}

	public Result<VirtualDevice> Register(VirtualDevice device)
	{
		lock (_gate)
		{
			if (device.Id.Length == 0 || device.Channels.Count == 0)
				return Result.Fail<VirtualDevice>("A device needs an id and at least one channel");

			foreach (ChannelRef channel in device.Channels)
			{
				if (!_sources.TryGetValue(channel.Source, out SourceDescriptor? source))
					return Result.Fail<VirtualDevice>($"No source called '{channel.Source}'");

				if (!source.Outputs.Contains(channel.Channel, StringComparer.OrdinalIgnoreCase))
					return Result.Fail<VirtualDevice>($"'{channel.Source}' has no output called '{channel.Channel}'");
			}

			_devices.RemoveAll(entry => entry.Id.Equals(device.Id, StringComparison.OrdinalIgnoreCase));
			_devices.Add(device);

			Save();
			return Result.Ok(device);
		}
	}

	public Result<VirtualSensor> Register(VirtualSensor sensor)
	{
		lock (_gate)
		{
			if (sensor.Id.Length == 0 || sensor.Source.Length == 0 || sensor.Channel.Length == 0)
				return Result.Fail<VirtualSensor>("A sensor needs an id, a source and a channel");

			if (!_sources.TryGetValue(sensor.Source, out SourceDescriptor? source))
				return Result.Fail<VirtualSensor>($"No source called '{sensor.Source}'");

			if (!source.Inputs.Contains(sensor.Channel, StringComparer.OrdinalIgnoreCase))
				return Result.Fail<VirtualSensor>($"'{sensor.Source}' has no input called '{sensor.Channel}'");

			if (_composites.Any(entry => entry.Id.Equals(sensor.Id, StringComparison.OrdinalIgnoreCase)))
				return Result.Fail<VirtualSensor>($"'{sensor.Id}' is already a composite");

			_sensors.RemoveAll(entry => entry.Id.Equals(sensor.Id, StringComparison.OrdinalIgnoreCase));
			_sensors.Add(sensor);

			Save();
			return Result.Ok(sensor);
		}
	}

	public Result<string> Unregister(string id)
	{
		lock (_gate)
		{
			string[] dependents = [.. _composites.Where(composite => composite.Inputs.Any(input =>
				input.Sensor.Equals(id, StringComparison.OrdinalIgnoreCase))).Select(composite => composite.Name)];
			if (dependents.Length > 0)
				return Result.Fail<string>($"Used by composites: {string.Join(", ", dependents)}. Edit or remove those first");

			int removed = _devices.RemoveAll(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
				+ _sensors.RemoveAll(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
				+ _composites.RemoveAll(entry => entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

			if (removed == 0) return Result.Fail<string>($"Nothing registered as '{id}'");

			_readings.Remove(id);

			Save();
			return Result.Ok($"'{id}' removed");
		}
	}

	public IReadOnlyList<ChannelView> Channels()
	{
		lock (_gate)
		{
			var views = new List<ChannelView>();

			foreach (SourceDescriptor source in _sources.Values)
			{
				foreach (string output in source.Outputs)
				{
					string[] taken =
					[
						.. _devices.Where(entry => entry.Channels.Any(channel =>
								channel.Source.Equals(source.Id, StringComparison.OrdinalIgnoreCase)
								&& channel.Channel.Equals(output, StringComparison.OrdinalIgnoreCase)))
							.Select(entry => entry.Name)
					];

					views.Add(new ChannelView(source.Id, output, true, taken.Length > 0,
						taken.Length > 0 ? string.Join(", ", taken) : null));
				}

				foreach (string input in source.Inputs)
				{
					VirtualSensor? taken = _sensors.FirstOrDefault(entry =>
						entry.Source.Equals(source.Id, StringComparison.OrdinalIgnoreCase)
						&& entry.Channel.Equals(input, StringComparison.OrdinalIgnoreCase));

					views.Add(new ChannelView(source.Id, input, false, taken is not null, taken?.Name));
				}
			}

			return views;
		}
	}

	// ---------- live state ----------

	public PowerState State(string device)
	{
		lock (_gate) return Believed(device);
	}

	public bool IsOn(string device) => State(device) == PowerState.On;

	/// A device is on while any of its outputs is on, so switching the room off leaves nothing lit
	private PowerState Believed(string device) =>
		Keys(device).Any(key => _states.GetValueOrDefault(key, PowerState.Off) == PowerState.On)
			? PowerState.On
			: PowerState.Off;

	/// Some outputs on and some off. Only a device spanning several channels can be in this state
	private bool Divided(string device)
	{
		string[] keys = Keys(device);
		if (keys.Length < 2) return false;

		return keys.Any(key => _states.GetValueOrDefault(key, PowerState.Off) == PowerState.On)
			&& keys.Any(key => _states.GetValueOrDefault(key, PowerState.Off) == PowerState.Off);
	}

	private string[] Keys(string device) =>
		_devices.FirstOrDefault(entry => entry.Id.Equals(device, StringComparison.OrdinalIgnoreCase)) is { } registered
			? [.. registered.Channels.Select(channel => $"{channel.Source}/{channel.Channel}")]
			: [device];

	public bool Set(string device, PowerState state)
	{
		lock (_gate)
		{
			var moved = false;

			foreach (string key in Keys(device))
			{
				if (_states.GetValueOrDefault(key, PowerState.Off) == state) continue;

				_states[key] = state;
				moved = true;
			}

			if (moved) repository.SaveChannels(_states);
			return moved;
		}
	}

	/// The device the desktop agent announces, whatever it ended up being called
	public VirtualDevice? Machine
	{
		get
		{
			lock (_gate)
				return _devices.FirstOrDefault(device => device.Channels
					.Any(channel => _sources.TryGetValue(channel.Source, out SourceDescriptor? source)
						&& source.Kind == SourceKind.Computer));
		}
	}

	/// What every output was last set to, so a restart can put the hardware back where it was
	public IReadOnlyDictionary<string, PowerState> Written
	{
		get { lock (_gate) return new Dictionary<string, PowerState>(_states, StringComparer.OrdinalIgnoreCase); }
	}

	/// Writes straight to the outputs and reports every device whose belief moved with them
	public IReadOnlyList<(string Device, PowerState State)> SetChannels(IReadOnlyList<ChannelRef> channels, PowerState state)
	{
		lock (_gate)
		{
			Dictionary<string, PowerState> before = _devices.ToDictionary(device => device.Id, device => Believed(device.Id));

			foreach (ChannelRef channel in channels) _states[$"{channel.Source}/{channel.Channel}"] = state;

			repository.SaveChannels(_states);

			return
			[
				.. _devices.Select(device => (device.Id, State: Believed(device.Id)))
					.Where(entry => before[entry.Id] != entry.State)
			];
		}
	}

	public SwitchAction Resolve(string device, SwitchAction action) =>
		action == SwitchAction.Toggle ? IsOn(device) ? SwitchAction.Off : SwitchAction.On : action;

	public Reading? Read(string sensor)
	{
		lock (_gate) return _readings.GetValueOrDefault(sensor);
	}

	public IReadOnlyList<string> Observe(string source, IReadOnlyDictionary<string, double> values, DateTimeOffset at)
	{
		var moved = new List<string>();

		lock (_gate)
		{
			_seen[source] = at;

			foreach ((string channel, double value) in values)
			{
				VirtualSensor? sensor = _sensors.FirstOrDefault(entry =>
					entry.Source.Equals(source, StringComparison.OrdinalIgnoreCase)
					&& entry.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

				if (sensor is null) continue;

				if (_readings.TryGetValue(sensor.Id, out Reading? previous) && Math.Abs(previous.Value - value) < double.Epsilon)
				{
					_readings[sensor.Id] = previous with { At = at };
					continue;
				}

				_readings[sensor.Id] = new Reading(value, at);
				moved.Add(sensor.Id);
			}
		}

		return moved;
	}

	public IReadOnlyList<DeviceView> Devices(Func<string, DateTimeOffset?> holdUntil)
	{
		(string Id, string Name, string IconOn, string IconOff, string Source, PowerState State, bool Partial)[] snapshot;

		lock (_gate)
			snapshot =
			[
				.. _devices.Select(device => (device.Id, device.Name, device.IconOn, device.IconOff,
					device.Channels.Count > 0 ? device.Channels[0].Source : "",
					Believed(device.Id), Divided(device.Id)))
			];

		return
		[
			.. snapshot.Select(device => new DeviceView(device.Id, device.Name,
				device.State == PowerState.On || device.IconOff.Length == 0 ? device.IconOn : device.IconOff,
				device.Source, device.State, holdUntil(device.Id), device.Partial))
		];
	}

	/// Everything readable, composites included, because to a reader they are sensors
	public IReadOnlyList<VirtualSensor> Registered
	{
		get { lock (_gate) return [.. _sensors, .. _composites.Select(AsSensor)]; }
	}

	/// Only what is registered on a hardware channel. Composites are edited as composites, so listing
	/// their stand-in sensors here would offer an edit that cannot be saved
	public IReadOnlyList<VirtualSensor> Declared
	{
		get { lock (_gate) return [.. _sensors]; }
	}

	private void Save() => repository.SaveRegistrations(new Registrations(_devices, _sensors, _composites));

	public IReadOnlyList<VirtualDevice> RegisteredDevices
	{
		get { lock (_gate) return [.. _devices]; }
	}

	public IReadOnlyList<SensorView> Sensors()
	{
		lock (_gate)
			return
			[
				.. _sensors.Concat(_composites.Select(AsSensor)).Select(sensor =>
				{
					Reading? reading = _readings.GetValueOrDefault(sensor.Id);
					return new SensorView(sensor.Id, sensor.Name, Icon(sensor, reading), sensor.Source,
						Render(sensor, reading), sensor.Unit, reading is not null, reading?.At,
						reading?.Value, sensor.Min, sensor.Max, sensor.Kind);
				})
			];
	}

	public IReadOnlyList<SourceView> Views()
	{
		lock (_gate)
			return
			[
				.. _sources.Values.Select(source => new SourceView(source.Id, source.Id, source.Kind,
					_seen.ContainsKey(source.Id) ? SourceState.Online : SourceState.Offline,
					_seen.GetValueOrDefault(source.Id) is var seen && seen == default ? null : seen,
					source.Outputs.Count, source.Inputs.Count,
					_facts.TryGetValue(source.Id, out List<SourceFact>? facts) ? [.. facts] : []))
			];
	}

	/// Only a boolean has two states to draw, and only if it was given a second icon to draw them with
	private static string Icon(VirtualSensor sensor, Reading? reading)
	{
		if (sensor.Kind != ReadingKind.Boolean || sensor.IconLow.Length == 0) return sensor.IconHigh;

		return reading is { Value: >= 0.5 } ? sensor.IconHigh : sensor.IconLow;
	}

	private static string Render(VirtualSensor sensor, Reading? reading) =>
		reading is null
			? ""
			: sensor.Kind == ReadingKind.Boolean
				? reading.Value >= 0.5 ? "true" : "false"
				: Units.Number(reading.Value);
}
