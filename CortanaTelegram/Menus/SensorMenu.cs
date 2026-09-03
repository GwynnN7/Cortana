using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaTelegram.Runtime;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// Every reading, and the features that can be switched from here
public sealed class SensorMenu : Menu
{
	private IReadOnlyList<PluginView> _features = [];
	private int _tab;

	public override string Tag => "sensor";

	public override int Topic => TelegramSession.Topics.Sensors;

	private static string Status(AutomationView automation) => automation.Status switch
	{
		AutomationStatus.Off => "🔴",
		AutomationStatus.Holding => $"✋ ~ {automation.HoldingUntil:HH:mm}",
		AutomationStatus.Idle => "🕸",
		_ => "🟢"
	};

	protected override async Task<string> Render()
	{
		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return "\n📡 <b>Sensors</b>\n====================\nCortana is offline\n";

		CortanaSnapshot state = snapshot.Value;
		_features = [.. state.Plugins.Where(view => view.CanDisable)];

		if (_tab == 1)
		{
			string switches = string.Join("\n", _features.Select(view =>
				$"{(view.Active ? "🟢" : "🔴")} • <b>{view.Name}</b> — {view.Purpose.ToLowerInvariant()}"));

			return $"\n🧩 <b>Features</b>\n====================\n{switches}\n";
		}

		string rows = string.Join("\n", state.Sensors.Select(view =>
			$"{view.Icon} • <b>{view.Name}:</b> {Reading(view)}"));

		AutomationView automation = state.Automation;

		return $"\n📡 <b>Sensors</b>\n====================\n{rows}\n\n" +
			$"⚙️ <b>Automation</b>\n=================\n" +
			$"🤖 • <b>Automation:</b> {Status(automation)}\n" +
			$"🛌 • <b>Sleep mode:</b> {(automation.SleepMode ? "🟢" : "🔴")}\n" +
			$"🕒 • <b>Context:</b> {automation.TimeContext}\n";
	}

	private static string Reading(SensorView view)
	{
		if (view is not { Available: true } || view.Value.Length == 0) return "unknown";

		return view.Value is "true" or "false"
			? view.Value == "true" ? "🟢" : "🔴"
			: $"{view.Value}{view.Unit}";
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		InlineKeyboardMarkup keyboard = Buttons();

		if (_tab == 0)
		{
			keyboard
				.AddButton("Automation 🤖", $"{Tag}-automation")
				.AddButton("Sleep 🛌", $"{Tag}-sleep")
				.AddNewRow();
		}
		else
		{
			var column = 0;
			foreach (PluginView view in _features)
			{
				keyboard.AddButton($"{view.Name} {(view.Active ? "🟢" : "🔴")}", $"{Tag}-flip-{view.Id}");
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

			case var _ when command.StartsWith("sensor-flip-"):
				string feature = command["sensor-flip-".Length..];

				TelegramSession.Toast(Topic, await TelegramSession.Text(
					TelegramSession.Cortana.SwitchPlugin(feature, SwitchAction.Toggle)));

				await Show(query);
				return;
		}
	}
}
