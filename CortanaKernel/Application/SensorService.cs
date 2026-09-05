using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

public sealed class SensorService(
	Fabric fabric,
	PresenceState presence,
	WarningState state,
	WarningStore warnings,
	SettingsStore flags,
	NotificationService notifications,
	IEventBus bus)
{
	public IReadOnlyList<SensorView> All() => fabric.Sensors();

	public IReadOnlyList<SourceView> Sources() => fabric.Views();

	public void Describe(string source, IReadOnlyDictionary<string, string> facts) => fabric.Describe(source, facts);

	public SourceView? Source(string source) =>
		fabric.Views().FirstOrDefault(view => view.Id.Equals(source, StringComparison.OrdinalIgnoreCase));

	public DateTimeOffset? SeenAt(string source) => Source(source)?.LastSeen;

	public double? Value(string sensor) => fabric.Read(sensor)?.Value;

	public Result<string> Read(string sensor)
	{
		SensorView? view = View(sensor);

		if (view is null) return Result.Fail<string>($"Unknown sensor '{sensor}'");
		return view.Available ? Result.Ok(view.Value) : Result.Fail<string>($"{view.Name} has no reading");
	}

	private SensorView? View(string sensor) =>
		fabric.Sensor(sensor) is { } registered
			? fabric.Sensors().FirstOrDefault(entry => entry.Sensor.Equals(registered.Id, StringComparison.OrdinalIgnoreCase))
			: null;

	public string Describe(string sensor)
	{
		SensorView? view = View(sensor);
		if (view is not { Available: true }) return $"{sensor} is unavailable";

		return fabric.Sensor(sensor)?.Kind == ReadingKind.Boolean
			? view.Value == "true" ? $"{view.Name} detected" : $"No {view.Name.ToLowerInvariant()}"
			: $"{view.Value}{view.Unit}";
	}

	public void Observe(string source, IReadOnlyDictionary<string, double> values, DateTimeOffset at)
	{
		VirtualSensor[] registered = [.. fabric.Registered.Where(sensor => sensor.Source.Equals(source, StringComparison.OrdinalIgnoreCase))];
		var readings = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

		foreach ((string channel, double raw) in values)
		{
			VirtualSensor? sensor = registered.FirstOrDefault(entry => entry.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
			readings[channel] = sensor is { Offset: not 0 } ? Math.Round(raw + sensor.Offset, 2) : raw;
		}

		bool wasOnline = fabric.IsOnline(source);
		bool wasReported = Reported();

		fabric.Observe(source, readings, at);

		// Only a sensor allowed to announce somebody moves the window. A sustaining one keeps presence
		// going through the engine, so a desk that woke on its own never backdates a person into it
		bool reported = Reported();
		if (reported) presence.LastMotionAt = at;

		if (!wasOnline) bus.Publish(new SensorAvailabilityChanged(true, at));
		if (reported && !wasReported) bus.Publish(new MotionDetected(at));

		EvaluateWarnings(at);
		bus.Publish(new SensorReadingReceived(at));
	}

	private bool Reported() =>
		fabric.Registered.Where(sensor => sensor.Presence == PresenceRole.Reports)
			.Any(sensor => fabric.Read(sensor.Id) is { Value: >= 0.5 });

	public void SetSourceOnline(string source, bool online)
	{
		if (fabric.IsOnline(source) == online) return;

		if (online) fabric.Touch(source);
		else fabric.Dropped(source);

		string name = fabric.Sources.FirstOrDefault(entry => entry.Id.Equals(source, StringComparison.OrdinalIgnoreCase))?.Id ?? source;

		bus.Publish(new SensorAvailabilityChanged(online, DateTimeOffset.Now));
		notifications.Raise(NotificationSource.Sensors, online ? $"{name} online" : $"{name} offline",
			online ? NotificationLevel.Info : NotificationLevel.Warning,
			online ? $"{name} reconnected and sent a reading" : $"{name} stopped sending readings");
	}

	private void EvaluateWarnings(DateTimeOffset now)
	{
		if (!flags.Flag(SettingKey.WarningsEnabled)) return;

		foreach (Warning warning in warnings.All())
		{
			if (!warning.Enabled) continue;

			bool active = state.IsActive(warning.Id);

			if (active && state.Since(warning.Id) is { } since && now - since >= TimeSpan.FromMinutes(warning.CooldownMinutes))
			{
				state.Clear(warning.Id);
				bus.Publish(new WarningStateChanged(warning.Id, false, now));
				continue;
			}

			if (!active && WarningRules.Fires(warning, fabric.Read))
			{
				state.Raise(warning.Id, now);
				notifications.Raise(NotificationSource.Warnings, warning.Message, warning.Level,
					WarningRules.Explain(warning, fabric.Read, fabric.Sensor));
				bus.Publish(new WarningStateChanged(warning.Id, true, now));
				continue;
			}

			if (active && WarningRules.Clears(warning, fabric.Read))
			{
				state.Clear(warning.Id);
				notifications.Raise(NotificationSource.Warnings, $"{warning.Name} back to normal",
					reason: WarningRules.Explain(warning, fabric.Read, fabric.Sensor));
				bus.Publish(new WarningStateChanged(warning.Id, false, now));
			}
		}
	}
}
