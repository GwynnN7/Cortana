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
	NotificationService notifications,
	IEventBus bus) : BackgroundService
{
	private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

	public VolitionState State => store.State;
	public bool Enabled => settings.Flag(SettingKey.VolitionEnabled);
	public string Status => !Enabled ? "Off until enabled" : VolitionRules.Quiet(State, DateTimeOffset.Now)
		? $"Quiet until {State.QuietUntil:HH:mm}" : "Free to speak";

	public Result<string> Quiet(int minutes)
	{
		if (minutes == 0)
			return settings.Write(SettingKey.VolitionEnabled, "Off")
				.Match(_ => Result.Ok("Volition off until enabled"), Result.Fail<string>);

		if (minutes is < 1 or > 1440) return Result.Fail<string>("Between 1 and 1440 minutes, or 0 to turn volition off until enabled");

		DateTimeOffset until = DateTimeOffset.Now.AddMinutes(minutes);
		store.Update(state => state with { QuietUntil = until });
		bus.Publish(new VolitionChanged(DateTimeOffset.Now));

		return Result.Ok(Status);
	}

	public Result<string> Speak()
	{
		store.Update(state => state with { QuietUntil = null });
		bus.Publish(new VolitionChanged(DateTimeOffset.Now));
		return Result.Ok(Enabled ? "Quiet period ended" : "Quiet period ended; volition remains off until enabled");
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

				await RunOnce(now, stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				Log.Error("Volition", ex.Message);
			}
		}
	}

	private async Task RunOnce(DateTimeOffset now, CancellationToken token = default)
	{
		token.ThrowIfCancellationRequested();
		await Consider(now, token);
		await WrapUp(now, token);
	}

	private bool CanSpeak(DateTimeOffset now) =>
		VolitionRules.CanSpeak(store.State, now, Enabled, automation.View().SleepMode);

	private async Task Consider(DateTimeOffset now, CancellationToken token)
	{
		int morning = settings.Number(SettingKey.MorningHour);
		if (!VolitionRules.ShouldGreet(store.State, now, morning, automation.View().SleepMode,
			Enabled, settings.Flag(SettingKey.MorningGreetingEnabled))) return;

		store.Update(state => state with { LastGreeted = DateOnly.FromDateTime(now.LocalDateTime) });

		string greeting = await Compose(now, token);
		token.ThrowIfCancellationRequested();
		if (!CanSpeak(DateTimeOffset.Now) || !settings.Flag(SettingKey.MorningGreetingEnabled)) return;

		store.Update(state => state with { LastSpokeAt = DateTimeOffset.Now });

		ai.Value.Append(Conversations.Web, greeting);
		notifications.Raise(NotificationSource.Cortana, greeting, reason: "the first morning greeting of the day");
	}

	/// The day, written down every evening. Whether she says it out loud is a coin toss
	private async Task WrapUp(DateTimeOffset now, CancellationToken token)
	{
		if (!settings.Flag(SettingKey.WrapupEnabled)) return;
		if (!VolitionRules.ShouldWrapUp(store.State, now, aiSettings.Integer(AiSettingKey.WrapupHour))) return;

		store.Update(state => state with { LastWrapped = DateOnly.FromDateTime(now.LocalDateTime) });

		history.Summarise(DateOnly.FromDateTime(now.LocalDateTime));

		IReadOnlyList<string> digest = history.Digest(now.Date, now);
		if (digest.Count == 0) return;

		// Keeping the day does not need a model. Only ask one when this wrap-up will actually be said.
		string summary = $"{now:yyyy-MM-dd}: {string.Join(", ", digest.Take(3))}";
		if (summary.Length > 400) summary = summary[..397] + "…";
		bool speak = CanSpeak(now) && Random.Shared.NextDouble() < aiSettings.Number(AiSettingKey.WrapupChance);

		if (speak) summary = await ai.Value.Compose(
			"This is what the house and the computer recorded today:\n" + string.Join("\n", digest) +
			Lately(now.Date, 10) +
			"\nWrite one or two short sentences about how the day went, in your own voice, as a note to yourself about gwynn7. " +
			"Pick what is actually worth remarking on and leave the rest out, and weigh what he said over what the " +
			"sensors counted. Do not list numbers he can read himself.",
			summary, token: token);

		token.ThrowIfCancellationRequested();
		if (settings.Flag(SettingKey.MemoryEnabled))
			memories.Remember(summary, MemoryKind.Day, "wrapup", TimeSpan.FromHours(aiSettings.Number(AiSettingKey.MemoryStateHours)));

		if (!speak || !CanSpeak(DateTimeOffset.Now) || !settings.Flag(SettingKey.WrapupEnabled)) return;

		store.Update(state => state with { LastSpokeAt = now });

		ai.Value.Append(Conversations.Web, summary);
		notifications.Raise(NotificationSource.Cortana, summary, reason: "the daily wrap-up");
	}

	private Task<string> Compose(DateTimeOffset now, CancellationToken token) => ai.Value.Compose(
		"It is the configured morning greeting window; this does not establish whether gwynn7 woke up or is home. This is what the house recorded overnight:\n" +
		string.Join("\n", history.Digest(now.AddHours(-8), now)) +
		Lately(now.AddDays(-1), 8) +
		"\nGreet him in one or two short sentences, in your own voice. Remark on the night only if something " +
		"about it is worth saying, and say nothing about the tools themselves. If the last thing he said means " +
		"he is not at home, greet somebody who is away rather than somebody who just got up. ",
		Greeting(now), token: token);

	/// The digest is what the house measured. This is what was actually said, and a plan only ever
	/// lives here - away for a few days, a bad night, an early start. A greeting composed from
	/// sensors alone greets whoever the thermometer thinks is in the room
	private string Lately(DateTimeOffset since, int turns)
	{
		string[] spoken =
		[
			.. ai.Value.History(Conversations.Web)
				.Where(turn => turn.At >= since)
				.TakeLast(turns)
				.Select(turn => $"{(turn.Mine ? "gwynn7" : "you")}: {turn.Text}")
		];

		return spoken.Length == 0 ? "" : "\nAnd this is what was said between you:\n" + string.Join("\n", spoken);
	}

	private string Greeting(DateTimeOffset now)
	{
		var parts = new List<string> { now.Hour < 12 ? "Good morning" : "Morning" };

		if (automation.View().WarningActive) parts.Add("a warning is firing");

		return string.Join(", ", parts);
	}
}
