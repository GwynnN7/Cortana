using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Sensors;
using CortanaKernel.Domain.Settings;
using CortanaKernel.Domain.Volition;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Application;

/// The one place Cortana decides to speak without being asked
public sealed class VolitionService(
	VolitionStore store,
	SettingsStore settings,
	AutomationService automation,
	SensorRegistry sensors,
	HistoryService history,
	Lazy<AiService> ai,
	NotificationService notifications) : BackgroundService
{
	private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

	public VolitionState State => store.State;

	public Result<string> Quiet(int minutes)
	{
		if (minutes is < 1 or > 1440) return Result.Fail<string>("Between 1 and 1440 minutes");

		DateTimeOffset until = DateTimeOffset.Now.AddMinutes(minutes);
		store.Update(state => state with { QuietUntil = until });

		return Result.Ok($"Quiet until {until:HH:mm}");
	}

	public Result<string> Speak()
	{
		store.Update(state => state with { QuietUntil = null });
		return Result.Ok("Back to normal");
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await Task.Delay(Tick, stoppingToken);
			}
			catch (OperationCanceledException)
			{
				return;
			}

			try
			{
				await Consider(DateTimeOffset.Now);
			}
			catch (Exception ex)
			{
				Log.Error("Volition", ex.Message);
			}
		}
	}

	private async Task Consider(DateTimeOffset now)
	{
		int morning = settings.Number(SettingKey.MorningHour);
		if (!VolitionRules.ShouldGreet(store.State, now, morning, automation.View().SleepMode)) return;

		store.Update(state => state with { LastGreeted = DateOnly.FromDateTime(now.LocalDateTime), LastSpokeAt = now });
		notifications.Raise(NotificationSource.Cortana, await Compose(now), reason: "the first morning greeting of the day");
	}

	private Task<string> Compose(DateTimeOffset now) => ai.Value.Compose(
		"It is morning and gwynn7 has not spoken to you yet. Greet him in one or two short sentences, in your own voice. " +
		"Look at the house first if something about the night or the state of the room is worth a remark, and say nothing about the tools themselves. " +
		"Do not ask him a question and do not offer a list of things you could do.",
		Greeting(now));

	private string Greeting(DateTimeOffset now)
	{
		var parts = new List<string> { now.Hour < 12 ? "Good morning" : "Morning" };

		if (Overnight("temperature", now) is { } temperature) parts.Add($"it got down to {Units.Number(temperature)}°C overnight");
		if (sensors.Last?.Co2 is { } co2) parts.Add(co2 >= settings.Number(SettingKey.Co2Threshold) ? $"the air is stale at {co2} ppm" : "the air is fine");

		return string.Join(", ", parts);
	}

	private double? Overnight(string metric, DateTimeOffset now)
	{
		var request = new AnalysisRequest(AnalysisFunction.Minimum, metric, now.AddHours(-8), now, null, null, null, null, 60);
		return history.Analyse(request).Match(result => result.Value, _ => null);
	}
}
