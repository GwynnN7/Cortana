using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
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
	AiSettingsStore aiSettings,
	AutomationService automation,
	HistoryService history,
	MemoryStore memories,
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
				DateTimeOffset now = DateTimeOffset.Now;

				await Consider(now);
				await WrapUp(now);
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

		string greeting = await Compose(now);

		ai.Value.Append(Conversations.Web, greeting);
		notifications.Raise(NotificationSource.Cortana, greeting, reason: "the first morning greeting of the day");
	}

	/// The day, written down every evening. Whether she says it out loud is a coin toss
	private async Task WrapUp(DateTimeOffset now)
	{
		if (!settings.Flag(SettingKey.WrapupEnabled)) return;
		if (!VolitionRules.ShouldWrapUp(store.State, now, aiSettings.Integer(AiSettingKey.WrapupHour))) return;

		store.Update(state => state with { LastWrapped = DateOnly.FromDateTime(now.LocalDateTime) });

		history.Summarise(DateOnly.FromDateTime(now.LocalDateTime));

		IReadOnlyList<string> digest = history.Digest(now.Date, now);
		if (digest.Count == 0) return;

		string summary = await ai.Value.Compose(
			"This is what the house and the computer recorded today:\n" + string.Join("\n", digest) +
			"\nWrite one or two short sentences about how the day went, in your own voice, as a note to yourself about gwynn7. " +
			"Pick what is actually worth remarking on and leave the rest out. Do not list numbers he can read himself.",
			string.Join(", ", digest.Take(3)));

		memories.Remember(summary, MemoryKind.State, "wrapup", ai.Value.StateLifetime);

		if (VolitionRules.Quiet(store.State, now) || automation.View().SleepMode) return;
		if (Random.Shared.NextDouble() > aiSettings.Number(AiSettingKey.WrapupChance)) return;

		store.Update(state => state with { LastSpokeAt = now });

		ai.Value.Append(Conversations.Web, summary);
		notifications.Raise(NotificationSource.Cortana, summary, reason: "the daily wrap-up");
	}

	private Task<string> Compose(DateTimeOffset now) => ai.Value.Compose(
		"It is morning and gwynn7 just woke up. This is what the house recorded overnight:\n" +
		string.Join("\n", history.Digest(now.AddHours(-8), now)) +
		"\nGreet him in one or two short sentences, in your own voice. Remark on the night only if something " +
		"about it is worth saying, and say nothing about the tools themselves. ",
		Greeting(now));

	private string Greeting(DateTimeOffset now)
	{
		var parts = new List<string> { now.Hour < 12 ? "Good morning" : "Morning" };

		if (automation.View().WarningActive) parts.Add("a warning is firing");

		return string.Join(", ", parts);
	}
}
