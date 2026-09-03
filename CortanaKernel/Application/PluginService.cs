using CortanaKernel.Domain.Ai;
using CortanaKernel.Domain.Common;
using CortanaKernel.Domain.Fabric;
using CortanaKernel.Domain.Notes;
using CortanaKernel.Domain.Settings;
using CortanaLib.Contracts;
using CortanaLib.Primitives;

namespace CortanaKernel.Application;

/// Every feature Cortana runs, what it is doing, and the switch behind it
public sealed class PluginService(
	SettingsStore settings,
	AiSettingsStore ai,
	AutomationService automation,
	Lazy<VolitionService> volition,
	Fabric fabric,
	BindStore binds,
	WarningStore warnings,
	MemoryStore memories,
	NoteStore notes)
{
	public IReadOnlyList<PluginView> All()
	{
		bool quiet = volition.Value.State.QuietUntil is { } until && until > DateTimeOffset.Now;

		return
		[
			new("fabric", "Fabric", "Sources, devices and sensors", true, false,
				$"{fabric.Sources.Count} sources, {fabric.RegisteredDevices.Count} devices, {fabric.Registered.Count} sensors"),
			new("settings", "Settings", "Runtime tuning", true, false, "always on"),
			new("automation", "Automation", "Evaluates binds",
				settings.Flag(SettingKey.AutomationEnabled), true,
				$"{binds.All().Count(bind => bind.Enabled)} of {binds.All().Count} binds enabled"),
			new("sleep", "Sleep", "The night state machine",
				settings.Flag(SettingKey.SleepEnabled), true,
				automation.View().SleepMode ? "sleeping" : "awake"),
			new("warnings", "Warnings", "Sensors against thresholds",
				settings.Flag(SettingKey.WarningsEnabled), true,
				$"{warnings.All().Count(warning => warning.Enabled)} of {warnings.All().Count} enabled"),
			new("memory", "Memory", "What she knows about you",
				settings.Flag(SettingKey.MemoryEnabled), true,
				$"{memories.All().Count} entries, {ai.Number(AiSettingKey.MemoryDepth):0} injected"),
			new("notes", "Notes", "What you asked her to note",
				settings.Flag(SettingKey.NotesEnabled), true,
				$"{notes.Open().Count} open of {notes.All().Count}"),
			new("volition", "Volition", "What she says unprompted", !quiet, true,
				quiet ? $"quiet until {volition.Value.State.QuietUntil:HH:mm}" : "free to speak"),
			new("wrapup", "Wrap-up", "The day, every evening",
				settings.Flag(SettingKey.WrapupEnabled), true,
				$"at {ai.Integer(AiSettingKey.WrapupHour):00}:00, said {ai.Number(AiSettingKey.WrapupChance) * 100:0}% of the time"),
			new("history", "History", "Records the house",
				settings.Flag(SettingKey.HistoryEnabled), true,
				$"every {ai.Number(AiSettingKey.HistorySampleMinutes):0} min"),
			new("push", "Push", "The status notification",
				settings.Flag(SettingKey.NotifyWeb), true,
				settings.Flag(SettingKey.NotifyWeb) ? "web push on" : "web push off"),
			new("telegram", "Telegram", "The Telegram bot",
				settings.Flag(SettingKey.NotifyTelegram), true, ""),
			new("discord", "Discord", "The Discord bot",
				settings.Flag(SettingKey.NotifyDiscord), true, "")
		];
	}

	public bool Active(string plugin) =>
		All().FirstOrDefault(view => view.Id.Equals(plugin, StringComparison.OrdinalIgnoreCase))?.Active ?? true;

	public Result<string> Switch(string plugin, SwitchAction action)
	{
		switch (plugin.ToLowerInvariant())
		{
			case "automation":
				return automation.SetAutomation(action, CommandOrigin.Internal with { Reason = "the services page" });

			case "volition":
				bool quiet = volition.Value.State.QuietUntil is { } until && until > DateTimeOffset.Now;
				bool speaking = action switch
				{
					SwitchAction.On => true,
					SwitchAction.Off => false,
					_ => quiet
				};

				return speaking ? volition.Value.Speak() : volition.Value.Quiet(1440);

			default:
				if (Behind(plugin) is not { } key) return Result.Fail<string>($"'{plugin}' has no switch of its own");

				Result<string> written = settings.Write(key, action.ToString());
				if (!written.IsOk) return written;

				string name = All().FirstOrDefault(view => view.Id == plugin)?.Name ?? plugin;
				return Result.Ok($"{name} is {(settings.Flag(key) ? "on" : "off")}");
		}
	}

	private static SettingKey? Behind(string plugin) => plugin.ToLowerInvariant() switch
	{
		"sleep" => SettingKey.SleepEnabled,
		"warnings" => SettingKey.WarningsEnabled,
		"memory" => SettingKey.MemoryEnabled,
		"notes" => SettingKey.NotesEnabled,
		"history" => SettingKey.HistoryEnabled,
		"wrapup" => SettingKey.WrapupEnabled,
		"push" => SettingKey.NotifyWeb,
		"telegram" => SettingKey.NotifyTelegram,
		"discord" => SettingKey.NotifyDiscord,
		_ => null
	};
}
