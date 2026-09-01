using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// Readings and the settings that drive automation
public sealed class SensorMenu : Menu
{
	private static readonly (SettingKey Key, string Label, string Prompt)[] Editable =
	[
		(SettingKey.LightThreshold, "Light 💡", "Light threshold in lux"),
		(SettingKey.Co2Threshold, "CO2 🧪", "CO2 threshold in ppm"),
		(SettingKey.TvocThreshold, "TVOC 🦠", "TVOC threshold in ppb"),
		(SettingKey.MorningHour, "Morning 🌅", "Morning hour, 0 to 23"),
		(SettingKey.NightHour, "Night 🌇", "Night hour, 0 to 23"),
		(SettingKey.MotionTimeoutSeconds, "Motion ⏳", "Seconds without motion before the lamp goes off, unless you are at the desk"),
		(SettingKey.ManualOverrideMinutes, "Manual ✋", "Minutes the day hold pauses automation"),
		(SettingKey.SleepManualOverrideMinutes, "Sleep touch 🌙", "Minutes the night hold pauses automation"),
		(SettingKey.SleepEntryDelayMinutes, "Sleep delay ⏱", "Minutes between the night starting and sleep mode"),
		(SettingKey.SleepHoldMinutes, "Sleep hold 🚫", "Minutes automatic sleep stays suppressed after manual override"),
		(SettingKey.DaySleepMinutes, "Day sleep 🛌", "Minutes a daytime sleep mode lasts")
	];

	private int _tab;

	private static string Status(AutomationView automation) => automation.Status switch
	{
		AutomationStatus.Off => "🔴",
		AutomationStatus.Holding => $"✋ ~ {automation.HoldingUntil:HH:mm}",
		_ => "🟢"
	};

	public override string Tag => "sensor";

	public override int Topic => TelegramSession.Topics.Sensors;

	protected override async Task<string> Render()
	{
		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return "\n📡 <b>Sensors</b>\n====================\nCortana is offline\n";

		CortanaSnapshot state = snapshot.Value;

		string Reading(SensorId sensor)
		{
			SensorView? view = state.Sensors.FirstOrDefault(entry => entry.Sensor == sensor);
			if (view is not { Available: true } || view.Value.Length == 0) return "Unknown";
			return sensor == SensorId.Motion ? view.Value == "true" ? "🟢" : "🔴" : $"{view.Value}{view.Unit}";
		}

		string MotionIcon() =>
			state.Sensors.FirstOrDefault(entry => entry.Sensor == SensorId.Motion)?.Value == "true" ? "💠" : "🔮";

		string Setting(SettingKey key) => state.Settings.FirstOrDefault(entry => entry.Setting == key)?.Value ?? "?";

		AutomationView automation = state.Automation;

		return $"\n📡 <b>Sensors</b>\n====================\n" +
			$"💡 • <b>Light:</b> {Reading(SensorId.Light)}\n" +
			$"🌡 • <b>Temperature:</b> {Reading(SensorId.Temperature)}\n" +
			$"💧 • <b>Humidity:</b> {Reading(SensorId.Humidity)}\n" +
			$"🧪 • <b>CO2:</b> {Reading(SensorId.Co2)}\n" +
			$"🦠 • <b>TVOC:</b> {Reading(SensorId.Tvoc)}\n" +
			$"{MotionIcon()} • <b>Motion:</b> {Reading(SensorId.Motion)}\n\n" +
			$"⚙️ <b>Automation</b>\n=================\n" +
			$"🤖 • <b>Automation:</b> {Status(automation)}\n" +
			$"🛌 • <b>Sleep mode:</b> {(automation.SleepMode ? "🟢" : "🔴")}\n" +
			$"🕒 • <b>Context:</b> {automation.TimeContext}\n" +
			$"💡 • <b>Light threshold:</b> {Setting(SettingKey.LightThreshold)}\n" +
			$"🌅 • <b>Morning:</b> {Setting(SettingKey.MorningHour)}  🌇 <b>Night:</b> {Setting(SettingKey.NightHour)}\n" +
			$"⏳ • <b>Motion timeout:</b> {Setting(SettingKey.MotionTimeoutSeconds)}s\n" +
			$"✋ • <b>Overrides:</b> {Setting(SettingKey.ManualOverrideMinutes)}/{Setting(SettingKey.SleepManualOverrideMinutes)} min\n";
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		InlineKeyboardMarkup keyboard = Buttons();

		if (_tab == 0)
		{
			keyboard
				.AddButton("Automation 🤖", $"{Tag}-automation")
				.AddButton("Sleep 🛌", $"{Tag}-sleep")
				.AddNewRow()
				.AddButton("Pulse relay ⚖️", $"{Tag}-pulse")
				.AddNewRow();
		}
		else
		{
			var column = 0;
			foreach ((SettingKey key, string label, _) in Editable)
			{
				keyboard.AddButton(label, $"{Tag}-edit-{key}");
				if (++column % 2 == 0) keyboard.AddNewRow();
			}

			if (column % 2 != 0) keyboard.AddNewRow();
		}

		return keyboard
			.AddButton("Refresh 🔄", $"{Tag}-refresh")
			.AddButton("Tab ↔️", $"{Tag}-tab");
	}

	public override async Task Handle(CallbackQuery query, string command)
	{
		switch (command)
		{
			case "sensor-refresh":
			case "sensor-cancel":
				await Show(query);
				return;

			case "sensor-tab":
				_tab = (_tab + 1) % 2;
				await Show(query);
				return;

			case "sensor-automation":
				await TelegramSession.Cortana.SetAutomation(SwitchAction.Toggle);
				await Show(query);
				return;

			case "sensor-sleep":
				await TelegramSession.Cortana.SetSleepMode(SwitchAction.Toggle);
				await Show(query);
				return;

			case "sensor-pulse":
				await TelegramSession.Cortana.SetSetting(SettingKey.LampUsesPulseRelay, "Toggle");
				await Show(query);
				return;

			case var _ when command.StartsWith("sensor-edit-"):
				string name = command["sensor-edit-".Length..];
				(SettingKey key, string _, string prompt) = Editable.First(entry => entry.Key.ToString() == name);

				if (TelegramSession.Begin(Topic, new PendingInput("setting", query, query.Message!, key.ToString()), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId, prompt,
						replyMarkup: TelegramSession.Cancel(Tag));

				return;
		}
	}

	public override async Task Handle(IncomingText message, PendingInput pending)
	{
		if (pending.Kind != "setting") return;

		string result = await TelegramSession.Text(
			TelegramSession.Cortana.SetSetting(Enum.Parse<SettingKey>(pending.Argument), message.Text.Trim()));

		await TelegramSession.Delete(message.MessageId);
		TelegramSession.Toast(Topic, result);
		await Show(pending.Query);
	}
}
