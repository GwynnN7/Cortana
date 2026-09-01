using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// Devices, the room, and sleep mode
public sealed class DeviceMenu : Menu
{
	private static readonly Dictionary<DeviceId, string> Emoji = new()
	{
		[DeviceId.Lamp] = "💡",
		[DeviceId.Computer] = "💻",
		[DeviceId.Power] = "⚡️",
		[DeviceId.Generic] = "🔌"
	};

	private DeviceId? _selected;
	private bool _withTimer;

	public override string Tag => "device";

	public override int Topic => TelegramSession.Topics.Devices;

	protected override async Task<string> Render()
	{
		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return "\n🏠 <b>Devices</b>\n================\nCortana is offline\n";

		string rows = string.Join("\n", snapshot.Value.Devices.Select(view =>
			$"{(view.State == PowerState.On ? "🟢" : "🔴")} • <b>{view.Device}</b> {Emoji[view.Device]}"));

		AutomationView automation = snapshot.Value.Automation;

		string state = automation.SleepMode
			? "🛌 Sleeping"
			: automation.Status switch
			{
				AutomationStatus.Off => "✋ Manual",
				AutomationStatus.Holding => $"✋ Holding until {automation.HoldingUntil:HH:mm}",
				_ => "🤖 Automatic"
			};

		return $"\n🏠 <b>Devices</b>\n================\n{rows}\n\n{state} · {automation.TimeContext}\n";
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		InlineKeyboardMarkup keyboard = Buttons();

		if (_selected is not null)
		{
			return keyboard
				.AddButton($"On 🟢", $"{Tag}-on")
				.AddButton("Off 🔴", $"{Tag}-off")
				.AddNewRow()
				.AddButton("Toggle 🔄", $"{Tag}-toggle")
				.AddNewRow()
				.AddButton(_withTimer ? "Set timer ✅" : "No timer ❌", $"{Tag}-timer")
				.AddNewRow()
				.AddButton("<<", $"{Tag}-cancel");
		}

		foreach (DeviceId entry in Enum.GetValues<DeviceId>())
			keyboard.AddButton($"{entry} {Emoji[entry]}", $"{Tag}-pick-{entry}").AddNewRow();

		return keyboard
			.AddButton("Room on 🏠", $"{Tag}-roomon")
			.AddButton("Room off 🌑", $"{Tag}-roomoff")
			.AddNewRow()
			.AddButton("Sleep 🛌", $"{Tag}-sleep")
			.AddButton("Automation 🤖", $"{Tag}-automation")
			.AddNewRow()
			.AddButton("Refresh 🔄", $"{Tag}-refresh");
	}

	public override async Task Handle(CallbackQuery query, string command)
	{
		switch (command)
		{
			case $"{nameof(DeviceMenu)}": return;

			case "device-refresh":
			case "device-cancel":
				_selected = null;
				_withTimer = false;
				await Show(query);
				return;

			case "device-timer":
				_withTimer = !_withTimer;
				await TelegramSession.Bot.EditMessageReplyMarkup(query.Message!.Chat.Id, query.Message.MessageId, Keyboard());
				return;

			case "device-sleep":
				await Report(query, await TelegramSession.Text(TelegramSession.Cortana.SetSleepMode(SwitchAction.Toggle)));
				return;

			case "device-automation":
				await Report(query, await TelegramSession.Text(TelegramSession.Cortana.SetAutomation(SwitchAction.Toggle)));
				return;

			case "device-roomon":
				await Report(query, await TelegramSession.Text(TelegramSession.Cortana.SwitchRoom(SwitchAction.On)));
				return;

			case "device-roomoff":
				await Report(query, await TelegramSession.Text(TelegramSession.Cortana.SwitchRoom(SwitchAction.Off)));
				return;

			case "device-on":
			case "device-off":
			case "device-toggle":
				await Apply(query, Enum.Parse<SwitchAction>(command.Split('-')[^1], true));
				return;

			case var _ when command.StartsWith("device-pick-"):
				_selected = Enum.Parse<DeviceId>(command["device-pick-".Length..], true);
				_withTimer = false;
				await TelegramSession.Bot.EditMessageReplyMarkup(query.Message!.Chat.Id, query.Message.MessageId, Keyboard());
				return;
		}
	}

	private async Task Apply(CallbackQuery query, SwitchAction action)
	{
		if (_selected is not { } device)
		{
			TelegramSession.Toast(Topic, "Pick a device first");
			return;
		}

		if (_withTimer)
		{
			if (TelegramSession.Begin(Topic, new PendingInput("timer", query, query.Message!, $"{device}:{action}"), query))
				await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
					"When? Use a pattern like 30s 10m 2h 1d", replyMarkup: TelegramSession.Cancel(Tag));

			return;
		}

		_selected = null;
		await Report(query, await TelegramSession.Text(TelegramSession.Cortana.SwitchDevice(device, action)));
	}

	public override async Task Handle(IncomingText message, PendingInput pending)
	{
		if (pending.Kind != "timer") return;

		TimeSpan? delay = TelegramSession.ParseDuration(message.Text);
		await TelegramSession.Delete(message.MessageId);

		if (delay == null)
		{
			TelegramSession.Toast(Topic, "That time pattern is not valid, try again");
			return;
		}

		string[] parts = pending.Argument.Split(':');
		var request = new CreateScheduleRequest(
			$"{parts[0]} {parts[1]}",
			ScheduleTrigger.Once,
			ScheduleActionType.SwitchDevice,
			parts[0],
			parts[1],
			DateTimeOffset.Now + delay.Value,
			Owner: "telegram");

		_selected = null;
		_withTimer = false;

		TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.CreateSchedule(request)));
		await Show(pending.Query);
	}

	private async Task Report(CallbackQuery query, string result)
	{
		TelegramSession.Toast(Topic, result);
		await Show(query);
	}
}
