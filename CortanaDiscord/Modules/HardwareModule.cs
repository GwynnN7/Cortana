using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Interactions;

namespace CortanaDiscord.Modules;

[Group("hardware", "Home automation")]
[RequireOwner]
public class HardwareModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("lamp", "Switch Lamp", runMode: RunMode.Async)]
	public Task LightToggle() => Reply(ApiHandler.Post($"{ERoute.Devices}/{EDevice.Lamp}"));

	[SlashCommand("device", "Switch Device", runMode: RunMode.Async)]
	public Task DeviceInteract([Summary("device", "Select Device")] EDevice device, [Summary("action", "Select Action")] ESwitchAction action) =>
		Reply(ApiHandler.Post($"{ERoute.Devices}/{device}", new PostAction(action.ToString())));

	[SlashCommand("device-info", "Get Device Status", runMode: RunMode.Async)]
	public Task DeviceStatus([Summary("device", "Select Device")] EDevice device) =>
		Reply(ApiHandler.Get($"{ERoute.Devices}/{device}"));

	[SlashCommand("room", "Switch the whole room", runMode: RunMode.Async)]
	public Task Room([Summary("action", "Select Action")] ESwitchAction action) =>
		Reply(ApiHandler.Post($"{ERoute.Devices}/room", new PostAction(action.ToString())));

	[SlashCommand("command-raspberry", "Interact with Raspberry", runMode: RunMode.Async)]
	public async Task CommandRaspberry(
		[Summary("option", "Select Option")] ERaspberryCommand command,
		[Summary("args", "Insert Argument")] string args = "",
		[Summary("confirm", "Required for Shutdown and Reboot")] EAnswer confirm = EAnswer.No)
	{
		if (command is ERaspberryCommand.Shutdown or ERaspberryCommand.Reboot && confirm != EAnswer.Yes)
		{
			await RespondAsync($"`{command}` takes Cortana offline and the Pi has no remote power switch. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(ApiHandler.Post($"{ERoute.Raspberry}", new PostCommand(command.ToString(), args)));
	}

	[SlashCommand("raspberry-info", "Get Raspberry Info", runMode: RunMode.Async)]
	public Task RaspberryInfo([Summary("info", "Select Info")] ERaspberryInfo info) =>
		Reply(ApiHandler.Get($"{ERoute.Raspberry}/{info}"));

	[SlashCommand("command-pc", "Interact with PC", runMode: RunMode.Async)]
	public async Task ComputerCommand(
		[Summary("command", "Select Command")] EComputerCommand command,
		[Summary("args", "Insert Argument")] string args = "",
		[Summary("confirm", "Required for Shutdown")] EAnswer confirm = EAnswer.No)
	{
		if (command == EComputerCommand.Shutdown && confirm != EAnswer.Yes)
		{
			await RespondAsync("`Shutdown` powers the desktop off. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(ApiHandler.Post($"{ERoute.Computer}", new PostCommand(command.ToString(), args)));
	}

	[SlashCommand("sensor", "Get Sensor Data", runMode: RunMode.Async)]
	public Task SensorData([Summary("info", "Select Data")] ESensor info) =>
		Reply(ApiHandler.Get($"{ERoute.Sensors}/{info}"));

	[SlashCommand("sensors", "Get every sensor reading", runMode: RunMode.Async)]
	public Task AllSensors() => Reply(ApiHandler.Get($"{ERoute.Sensors}"));

	[SlashCommand("pc-metrics", "Get PC performance and temperatures", runMode: RunMode.Async)]
	public Task ComputerMetrics() => Reply(ApiHandler.Get($"{ERoute.Computer}/metrics"));

	[SlashCommand("llm-models", "Show every selectable language model", runMode: RunMode.Async)]
	public Task LlmModels() => Reply(ApiHandler.Get($"{ERoute.AI}/models"));

	[SlashCommand("llm-model", "Switch the language model", runMode: RunMode.Async)]
	public Task LlmModel([Summary("model", "Select Model")] ELlmModel model) =>
		Reply(ApiHandler.Post($"{ERoute.AI}/model", new PostModel(model.ToString())));

	[SlashCommand("llm-settings", "Show the AI settings", runMode: RunMode.Async)]
	public Task LlmSettings() => Reply(ApiHandler.Get($"{ERoute.AI}/settings"));

	[SlashCommand("llm-set", "Change an AI setting", runMode: RunMode.Async)]
	public Task LlmSet([Summary("setting", "Select Setting")] EAiSetting setting, [Summary("value", "New value")] double value) =>
		Reply(ApiHandler.Post($"{ERoute.AI}/settings/{setting}", new PostNumber(value)));

	[SlashCommand("llm-prompt", "Show the system prompt", runMode: RunMode.Async)]
	public Task LlmPrompt() => Reply(ApiHandler.Get($"{ERoute.AI}/prompt"));

	[SlashCommand("llm-prompt-set", "Replace the system prompt", runMode: RunMode.Async)]
	public Task LlmPromptSet([Summary("prompt", "New system prompt")] string prompt) =>
		Reply(ApiHandler.Post($"{ERoute.AI}/prompt", new PostPrompt(prompt)));

	[SlashCommand("llm-prompt-reset", "Restore the system prompt that ships with Cortana", runMode: RunMode.Async)]
	public Task LlmPromptReset() => Reply(ApiHandler.Delete($"{ERoute.AI}/prompt"));

	[SlashCommand("sleep", "Enter Sleep Mode", runMode: RunMode.Async)]
	public Task Sleep() => Reply(ApiHandler.Post($"{ERoute.Devices}/sleep"));

	[SlashCommand("settings", "Show every automation setting", runMode: RunMode.Async)]
	public Task ShowSettings() => Reply(ApiHandler.Get($"{ERoute.Sensors}/settings"));

	[SlashCommand("set", "Change an automation setting", runMode: RunMode.Async)]
	public Task SetSetting([Summary("setting", "Select Setting")] ESettings setting, [Summary("value", "New value")] int value) =>
		Reply(ApiHandler.Post($"{ERoute.Sensors}/settings/{setting}", new PostValue(value)));

	[SlashCommand("status", "Show which subfunctions are running", runMode: RunMode.Async)]
	public async Task Subfunctions()
	{
		await DeferAsync(true);

		IOption<SubfunctionListResponse> statuses = await ApiHandler.Get<SubfunctionListResponse>($"{ERoute.SubFunctions}");

		Embed embed = statuses.Match(
			list =>
			{
				EmbedBuilder builder = DiscordUtils.CreateEmbed("Subfunctions").ToEmbedBuilder();
				foreach (SubfunctionResponse status in list.Subfunctions)
					builder.AddField(status.Subfunction.Replace("Cortana", ""), status.Running ? "🟢 Running" : "🔴 Stopped", inline: true);
				return builder.Build();
			},
			() => DiscordUtils.CreateEmbed("Cortana is offline"));

		await FollowupAsync(embed: embed, ephemeral: true);
	}

	[SlashCommand("schedules", "List the persistent schedules", runMode: RunMode.Async)]
	public Task Schedules() => Reply(ApiHandler.Get($"{ERoute.Schedules}"));

	[SlashCommand("schedule-run", "Run a schedule now", runMode: RunMode.Async)]
	public Task RunSchedule([Summary("id", "Schedule id")] string id) =>
		Reply(ApiHandler.Post($"{ERoute.Schedules}/{id}", new PostScheduleUpdate("run")));

	[SlashCommand("schedule-delete", "Delete a schedule", runMode: RunMode.Async)]
	public Task DeleteSchedule([Summary("id", "Schedule id")] string id) =>
		Reply(ApiHandler.Delete($"{ERoute.Schedules}/{id}"));

	[SlashCommand("subfunction", "Start, stop, restart or update a subfunction", runMode: RunMode.Async)]
	public async Task ControlSubfunction(
		[Summary("subfunction", "Which one")] ESubFunctionType subfunction,
		[Summary("action", "What to do")] ESubfunctionAction action,
		[Summary("confirm", "Required to stop the Kernel")] EAnswer confirm = EAnswer.No)
	{
		if (subfunction == ESubFunctionType.CortanaKernel && action == ESubfunctionAction.Stop && confirm != EAnswer.Yes)
		{
			await RespondAsync("Stopping the Kernel cascades to every subfunction, including this bot. Re-run with `confirm: Yes`.", ephemeral: true);
			return;
		}

		await Reply(ApiHandler.Post($"{ERoute.SubFunctions}/{subfunction}", new PostAction(action.ToString())));
	}

	private async Task Reply(Task<string> call)
	{
		await DeferAsync(true);
		string result = await call;
		await FollowupAsync(embed: DiscordUtils.CreateEmbed(result), ephemeral: true);
	}
}
