using System.Globalization;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Domain.Sensors;

/// One observation from the station
public sealed record SensorReading(
	bool Motion,
	int Light,
	double Temperature,
	double Humidity,
	int Co2,
	int Tvoc,
	DateTimeOffset At)
{
	/// The station carries two temperature sensors: the SHT4x above is the room measurement, and this is the AHT20 that compensates the air-quality sensor
	public double? AirQualityTemperature { get; init; }
}

/// Keeps the last observation
public sealed class SensorRegistry
{
	private readonly Lock _gate = new();

	private SensorReading? _last;
	private bool _online;

	public bool Online
	{
		get { lock (_gate) return _online; }
	}

	public SensorReading? Last
	{
		get { lock (_gate) return _last; }
	}

	public DateTimeOffset? LastMotionAt { get; private set; }

	public bool AirQualityWarning { get; private set; }

	public DateTimeOffset? AirQualityWarningUntil { get; set; }

	public void SetOnline(bool online)
	{
		lock (_gate) _online = online;
	}

	public void Observe(SensorReading reading)
	{
		lock (_gate)
		{
			_last = reading;
			_online = true;
		}

		if (reading.Motion) LastMotionAt = reading.At;
	}

	public void SetAirQualityWarning(bool warning) => AirQualityWarning = warning;

	public double? Temperature => Last?.Temperature;
	public int? Light => Last?.Light;
	public bool? Motion => Last?.Motion;

	public string Value(SensorId sensor)
	{
		SensorReading? reading = Last;
		if (reading == null) return "";

		return sensor switch
		{
			SensorId.Temperature => Units.Number(reading.Temperature),
			SensorId.Humidity => Units.Number(reading.Humidity),
			SensorId.Light => reading.Light.ToString(CultureInfo.InvariantCulture),
			SensorId.Co2 => reading.Co2.ToString(CultureInfo.InvariantCulture),
			SensorId.Tvoc => reading.Tvoc.ToString(CultureInfo.InvariantCulture),
			SensorId.Motion => reading.Motion ? "true" : "false",
			_ => ""
		};
	}

	public IReadOnlyList<SensorView> All()
	{
		SensorReading? reading = Last;
		bool online = Online;

		return
		[
			.. Enum.GetValues<SensorId>().Select(sensor =>
				new SensorView(sensor, Value(sensor), Units.For(sensor), online && reading != null, reading?.At))
		];
	}
}
