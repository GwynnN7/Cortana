using CortanaDiscord.Runtime;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using Discord;
using Discord.Interactions;

namespace CortanaDiscord.Modules;

[Group("home", "Home automation")]
[RequireOwner]
public sealed class HomeModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("state", "Devices, sensors and automation at a glance", runMode: RunMode.Async)]
	public Task State() => Reply(DiscordContext.Cortana.GetText("snapshot"));

	[SlashCommand("device", "Switch a device on, off or toggle it", runMode: RunMode.Async)]
	public Task Device([Summary("device", "Which device")] string device, [Summary("action", "What to do")] SwitchAction action) =>
		Reply(DiscordContext.Cortana.SwitchDevice(device, action));

	[SlashCommand("devices", "Power state of every device", runMode: RunMode.Async)]
	public Task Devices() => Reply(DiscordContext.Cortana.Devices());

	[SlashCommand("sleep", "Turn sleep mode on, off or toggle it", runMode: RunMode.Async)]
	public Task Sleep([Summary("action", "On, Off or Toggle")] SwitchAction action = SwitchAction.Toggle) =>
		Reply(DiscordContext.Cortana.SetSleepMode(action));

	[SlashCommand("automation", "Turn autonomous automation on, off or toggle it", runMode: RunMode.Async)]
	public Task Automation([Summary("action", "On, Off or Toggle")] SwitchAction action = SwitchAction.Toggle) =>
		Reply(DiscordContext.Cortana.SetAutomation(action));

	[SlashCommand("resume", "Cancel a manual hold and give automation control back now", runMode: RunMode.Async)]
	public Task Resume() => Reply(DiscordContext.Cortana.ReleaseHolds());

	[SlashCommand("why", "Explain what automation last decided and why", runMode: RunMode.Async)]
	public Task Why() => Reply(DiscordContext.Cortana.GetText("automation/diagnostics"));

	[SlashCommand("sensors", "Every sensor reading", runMode: RunMode.Async)]
	public Task Sensors() => Reply(DiscordContext.Cortana.Sensors());

	[SlashCommand("sensor", "One sensor reading", runMode: RunMode.Async)]
	public Task Sensor([Summary("sensor", "Which sensor")] string sensor) =>
		Reply(DiscordContext.Cortana.Sensor(sensor));

	[SlashCommand("features", "Every feature Cortana runs and whether it is on", runMode: RunMode.Async)]
	public Task Features() => Reply(DiscordContext.Cortana.GetText("plugins"));

	[SlashCommand("feature", "Turn one feature on, off or toggle it", runMode: RunMode.Async)]
	public Task Feature([Summary("feature", "For example automation, warnings, notes")] string feature,
		[Summary("action", "On, Off or Toggle")] SwitchAction action = SwitchAction.Toggle) =>
		Reply(DiscordContext.Cortana.SwitchPlugin(feature, action));

	[SlashCommand("computer", "Command the desktop computer", runMode: RunMode.Async)]
	public async Task Computer(
		[Summary("command", "What to do")] ComputerCommand command,
		[Summary("argument", "Text, shell command or application name")] string argument = "",
		[Summary("confirm", "Required for Shutdown")] Answer confirm = Answer.No)
	{
		if (command == ComputerCommand.Shutdown && confirm != Answer.Yes)
		{
			await RespondAsync("`Shutdown` powers the desktop off. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(DiscordContext.Cortana.Computer(command, argument));
	}

	[SlashCommand("computer-metrics", "Load and temperatures of the desktop", runMode: RunMode.Async)]
	public Task ComputerMetrics() => Reply(DiscordContext.Cortana.GetText("metrics/computer"));

	[SlashCommand("raspberry", "One Raspberry Pi property", runMode: RunMode.Async)]
	public Task Raspberry([Summary("info", "Which property")] RaspberryInfo info) =>
		Reply(DiscordContext.Cortana.RaspberryInfo(info));

	[SlashCommand("raspberry-command", "Shut down, reboot or run a command on the Pi", runMode: RunMode.Async)]
	public async Task CommandRaspberry(
		[Summary("command", "What to do")] RaspberryCommand command,
		[Summary("argument", "The shell command, when running one")] string argument = "",
		[Summary("confirm", "Required for Shutdown and Reboot")] Answer confirm = Answer.No)
	{
		if (command is RaspberryCommand.Shutdown or RaspberryCommand.Reboot && confirm != Answer.Yes)
		{
			await RespondAsync($"`{command}` takes Cortana offline and the Pi has no remote power switch. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(DiscordContext.Cortana.Raspberry(command, argument));
	}
	private async Task Reply(Task<Result<string>> call)
	{
		await DeferAsync(true);
		await FollowupAsync(embed: DiscordContext.Card(await DiscordContext.Text(call)), ephemeral: true);
	}
}

[Group("cortana", "Cortana herself")]
[RequireOwner]
public sealed class CortanaModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("history", "How a recorded value moved over the last hours", runMode: RunMode.Async)]
	public async Task History([Summary("metric", "For example temperature, co2, lamp")] string metric, [Summary("hours", "How far back")] int hours = 24)
	{
		await DeferAsync(true);

		Result<HistorySeries> series = await DiscordContext.Cortana.History(metric, hours);
		await FollowupAsync(embed: DiscordContext.Card(series.Match(
			found => $"{found.Metric}: min {found.Min}{found.Unit}, max {found.Max}{found.Unit}, average {found.Average}{found.Unit}",
			error => error)), ephemeral: true);
	}

	[SlashCommand("analyse", "Run an exact calculation over recorded data", runMode: RunMode.Async)]
	public async Task Analyse(
		[Summary("function", "Which calculation")] AnalysisFunction function,
		[Summary("metric", "Which metric")] string metric,
		[Summary("hours", "How many hours back to look")] int hours = 24)
	{
		await DeferAsync(true);

		DateTimeOffset now = DateTimeOffset.Now;
		Result<AnalysisResult> result = await DiscordContext.Cortana.Analyse(
			new AnalysisRequest(function, metric, now.AddHours(-Math.Abs(hours)), now));

		await FollowupAsync(embed: DiscordContext.Card(result.Match(found => found.Summary, error => error)), ephemeral: true);
	}

	[SlashCommand("schedules", "Every saved schedule", runMode: RunMode.Async)]
	public Task Schedules() => Reply(DiscordContext.Cortana.SchedulesText());

	[SlashCommand("schedule-run", "Run a schedule now", runMode: RunMode.Async)]
	public Task RunSchedule([Summary("id", "Schedule id")] string id) =>
		Reply(DiscordContext.Cortana.CommandSchedule(id, "run"));

	[SlashCommand("schedule-delete", "Delete a schedule", runMode: RunMode.Async)]
	public Task DeleteSchedule([Summary("id", "Schedule id")] string id) =>
		Reply(DiscordContext.Cortana.DeleteSchedule(id));

	[SlashCommand("services", "Which Cortana services are running", runMode: RunMode.Async)]
	public async Task Services()
	{
		await DeferAsync(true);

		Result<CortanaSnapshot> snapshot = await DiscordContext.Cortana.Snapshot();

		Embed embed = snapshot.Match(
			state =>
			{
				EmbedBuilder builder = DiscordContext.Card("Services").ToEmbedBuilder();
				foreach (ServiceView view in state.Services)
					builder.AddField(view.Service.ToString(), view.Running ? "🟢 Running" : "🔴 Stopped", inline: true);

				return builder.Build();
			},
			error => DiscordContext.Card(error));

		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("service", "Start, stop, restart or update a service", runMode: RunMode.Async)]
	public async Task Service(
		[Summary("service", "Which one")] ServiceId service,
		[Summary("action", "What to do")] ServiceAction action,
		[Summary("confirm", "Required to stop the Kernel")] Answer confirm = Answer.No)
	{
		if (service == ServiceId.Kernel && action == ServiceAction.Stop && confirm != Answer.Yes)
		{
			await RespondAsync("Stopping the Kernel cascades to every other service. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(DiscordContext.Cortana.ControlService(service, action));
	}

	[SlashCommand("model", "Switch the language model", runMode: RunMode.Async)]
	public Task Model([Summary("model", "Which family")] LlmFamily model) =>
		Reply(DiscordContext.Cortana.SetModel(model.ToString()));

	[SlashCommand("models", "Every selectable language model", runMode: RunMode.Async)]
	public Task Models() => Reply(DiscordContext.Cortana.ModelsText());

	[SlashCommand("notes", "Everything written down", runMode: RunMode.Async)]
	public Task Notes() => Reply(DiscordContext.Cortana.GetText("notes"));

	private async Task Reply(Task<Result<string>> call)
	{
		await DeferAsync(true);
		await FollowupAsync(embed: DiscordContext.Card(await DiscordContext.Text(call)), ephemeral: true);
	}
}
