using CortanaKernel.Domain.Automation;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Sensors;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// Turns raw station observations into domain facts
public sealed class SensorService(
	SensorRegistry sensors,
	SettingsStore settings,
	NotificationService notifications,
	IEventBus bus)
{
	private static readonly TimeSpan AirQualityCooldown = TimeSpan.FromMinutes(45);

	public IReadOnlyList<SensorView> All() => sensors.All();

	public Result<string> Read(SensorId sensor)
	{
		string value = sensors.Value(sensor);
		return value.Length == 0 ? Result.Fail<string>("The station is offline") : Result.Ok(value);
	}

	public string Describe(SensorId sensor)
	{
		string value = sensors.Value(sensor);
		if (value.Length == 0) return $"{sensor} is unavailable";

		return sensor switch
		{
			SensorId.Motion => value == "true" ? "Motion detected" : "No motion",
			_ => $"{value}{Units.For(sensor)}"
		};
	}

	public void Observe(SensorReading raw)
	{
		double offset = settings.Decimal(SettingKey.TemperatureOffset);
		SensorReading reading = offset == 0 ? raw : raw with { Temperature = Math.Round(raw.Temperature + offset, 2) };

		bool wasOnline = sensors.Online;
		bool hadMotion = sensors.Motion == true;

		sensors.Observe(reading);

		if (!wasOnline) bus.Publish(new SensorAvailabilityChanged(true, reading.At));
		if (reading.Motion && !hadMotion) bus.Publish(new MotionDetected(reading.At));

		EvaluateAirQuality(reading);
		bus.Publish(new SensorReadingReceived(reading.At));
	}

	public string CalibrationNote()
	{
		SensorReading? reading = sensors.Last;
		if (reading?.AirQualityTemperature is not { } airQuality) return "";

		double offset = settings.Decimal(SettingKey.TemperatureOffset);
		double room = reading.Temperature - offset;

		return $"Room sensor {Units.Number(room)}°C uncorrected, air-quality sensor {Units.Number(airQuality)}°C, " +
			$"a {Units.Number(airQuality - room)}°C spread. Current offset {Units.Number(offset)}°C.";
	}

	public void SetStationOnline(bool online)
	{
		if (sensors.Online == online) return;

		sensors.SetOnline(online);
		bus.Publish(new SensorAvailabilityChanged(online, DateTimeOffset.Now));
		notifications.Raise(NotificationSource.Sensors, online ? "Station online" : "Station offline",
			online ? NotificationLevel.Info : NotificationLevel.Warning,
			online ? "the station reconnected and sent a reading" : "the station stopped sending readings");
	}

	private void EvaluateAirQuality(SensorReading reading)
	{
		DateTimeOffset now = reading.At;

		if (sensors.AirQualityWarning && sensors.AirQualityWarningUntil is { } until && until <= now)
		{
			sensors.SetAirQualityWarning(false);
			sensors.AirQualityWarningUntil = null;
			bus.Publish(new AirQualityWarningChanged(false, now));
		}

		if (!reading.Motion) return;

		int co2Threshold = settings.Number(SettingKey.Co2Threshold);
		int tvocThreshold = settings.Number(SettingKey.TvocThreshold);

		if (!sensors.AirQualityWarning && AutomationRules.AirQualityUnsafe(reading.Co2, reading.Tvoc, co2Threshold, tvocThreshold))
		{
			sensors.SetAirQualityWarning(true);
			sensors.AirQualityWarningUntil = now + AirQualityCooldown;
			notifications.Raise(NotificationSource.AirQuality, "Air quality low, open the window", NotificationLevel.Alert,
				$"CO2 {reading.Co2} ppm against a {co2Threshold} ppm threshold, TVOC {reading.Tvoc} ppb against {tvocThreshold} ppb");
			bus.Publish(new AirQualityWarningChanged(true, now));
			return;
		}

		if (sensors.AirQualityWarning && AutomationRules.AirQualityBackToNormal(reading.Co2, reading.Tvoc, co2Threshold, tvocThreshold))
		{
			sensors.SetAirQualityWarning(false);
			sensors.AirQualityWarningUntil = null;
			notifications.Raise(NotificationSource.AirQuality, "Air quality normal",
				reason: $"CO2 back to {reading.Co2} ppm and TVOC to {reading.Tvoc} ppb");
			bus.Publish(new AirQualityWarningChanged(false, now));
		}
	}
}
