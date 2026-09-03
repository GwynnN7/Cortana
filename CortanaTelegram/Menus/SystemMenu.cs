using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaTelegram.Runtime;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace CortanaTelegram.Menus;

/// The desktop on the first tab, the Raspberry on the second
public sealed class SystemMenu : Menu
{
	private int _tab;

	public override string Tag => "system";

	public override int Topic => TelegramSession.Topics.System;

	protected override Task<string> Render() => _tab == 0 ? Computer() : Raspberry();

	private static Task<string> Computer() => Machine(SourceIds.Computer, "🖥");

	private static async Task<string> Raspberry()
	{
		string machine = await Machine(SourceIds.Raspberry, "🍓");

		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return machine;

		string Value(RaspberryInfo info)
		{
			RaspberryInfoView? view = snapshot.Value.Raspberry.FirstOrDefault(entry => entry.Info == info);
			return view == null || view.Value.Length == 0 ? "Unknown" : $"{view.Value}{view.Unit}";
		}

		return machine +
			$"📍 • <b>Location</b>: {Value(RaspberryInfo.Location)}\n" +
			$"📬 • <b>IP</b>: {Value(RaspberryInfo.PublicIp)}\n";
	}

	/// Whatever the source says about itself, then whatever it is reading
	private static async Task<string> Machine(string source, string emoji)
	{
		Result<CortanaSnapshot> snapshot = await TelegramSession.Cortana.Snapshot();
		if (!snapshot.IsOk) return $"\n{emoji} <b>{source}</b>\n================\nCortana is offline\n";

		CortanaSnapshot state = snapshot.Value;
		SourceView? view = state.Sources.FirstOrDefault(entry => entry.Id == source);
		if (view is null) return $"\n{emoji} <b>{source}</b>\n================\nNothing announced yet\n";

		string name = view.Facts.FirstOrDefault(fact => fact.Key == "name")?.Value ?? view.Id;

		string facts = string.Join("\n", view.Facts
			.Where(fact => fact.Key != "name")
			.Select(fact => $"• <b>{fact.Key}</b>: {fact.Value}"));

		string readings = string.Join("\n", state.Sensors
			.Where(sensor => sensor.Source == source && sensor.Available)
			.Select(sensor => $"{sensor.Icon} • <b>{sensor.Name}</b>: {sensor.Value}{sensor.Unit}"));

		return $"\n{emoji} <b>{name}</b>\n================\n" +
			(facts.Length > 0 ? facts + "\n" : "") +
			(readings.Length > 0 ? readings + "\n" : "");
	}

	protected override InlineKeyboardMarkup Keyboard()
	{
		InlineKeyboardMarkup keyboard = Buttons();

		if (_tab == 0)
		{
			keyboard
				.AddButton("Reboot 🔄", $"{Tag}-pc-Reboot")
				.AddButton("Other OS 🎮", $"{Tag}-pc-BootIntoOtherOperatingSystem")
				.AddNewRow()
				.AddButton("Suspend 🌙", $"{Tag}-pc-Suspend")
				.AddButton("Shutdown ⏻", $"{Tag}-pc-Shutdown")
				.AddNewRow()
				.AddButton("Notify 📢", $"{Tag}-ask-Notify")
				.AddButton("Command 💻", $"{Tag}-ask-RunShellCommand")
				.AddNewRow()
				.AddButton("Launch 🚀", $"{Tag}-ask-LaunchApplication")
				.AddButton("Close ✖️", $"{Tag}-ask-CloseApplication")
				.AddNewRow();
		}
		else
		{
			keyboard
				.AddButton("Shutdown ⚡️", $"{Tag}-pi-Shutdown")
				.AddButton("Reboot 🔁", $"{Tag}-pi-Reboot")
				.AddNewRow()
				.AddButton("Command 💻", $"{Tag}-shell")
				.AddNewRow();
		}

		return keyboard
			.AddButton("Refresh 🔄", $"{Tag}-refresh")
			.AddButton("Tab ↔️", $"{Tag}-tab");
	}

	public override async Task Handle(CallbackQuery query, string command)
	{
		switch (command)
		{
			case "system-refresh":
				await Show(query);
				return;

			case "system-tab":
				_tab = (_tab + 1) % 2;
				await Show(query);
				return;

			case "system-cancel":
				if (TelegramSession.Pending.TryGetValue(Topic, out PendingInput? open) && open.Transcript.Count > 0)
					foreach (int id in open.Transcript) await TelegramSession.Delete(id);

				await Show(query);
				return;

			case "system-shell":
				if (TelegramSession.Begin(Topic, new PendingInput("shell", query, query.Message!), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						"Shell session open, every message runs on the Pi", replyMarkup: TelegramSession.Cancel(Tag));

				return;

			case var _ when command.StartsWith("system-pc-"):
				ComputerCommand pc = Enum.Parse<ComputerCommand>(command["system-pc-".Length..], true);
				TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.Computer(pc)));
				return;

			case var _ when command.StartsWith("system-pi-"):
				RaspberryCommand pi = Enum.Parse<RaspberryCommand>(command["system-pi-".Length..], true);
				TelegramSession.Toast(Topic, await TelegramSession.Text(TelegramSession.Cortana.Raspberry(pi)));
				return;

			case var _ when command.StartsWith("system-ask-"):
				string wanted = command["system-ask-".Length..];
				if (TelegramSession.Begin(Topic, new PendingInput("computer", query, query.Message!, wanted), query))
					await TelegramSession.Bot.EditMessageText(query.Message!.Chat.Id, query.Message.MessageId,
						Prompt(wanted), replyMarkup: TelegramSession.Cancel(Tag));

				return;
		}
	}

	private static string Prompt(string command) => command switch
	{
		nameof(ComputerCommand.Notify) => "What should I put on screen?",
		nameof(ComputerCommand.LaunchApplication) => "Which application should I start?",
		nameof(ComputerCommand.CloseApplication) => "Which application should I close?",
		_ => "Commands session open, every message runs on the computer"
	};

	public override async Task Handle(IncomingText message, PendingInput pending)
	{
		if (pending.Kind == "shell")
		{
			string output = await TelegramSession.Text(TelegramSession.Cortana.Raspberry(RaspberryCommand.RunShellCommand, message.Text));
			Message sent = await TelegramSession.Post(output, Topic);

			pending.Transcript.Add(message.MessageId);
			pending.Transcript.Add(sent.MessageId);
			return;
		}

		if (pending.Kind != "computer") return;

		var command = Enum.Parse<ComputerCommand>(pending.Argument, true);
		string result = await TelegramSession.Text(TelegramSession.Cortana.Computer(command, message.Text));

		if (command == ComputerCommand.RunShellCommand)
		{
			Message sent = await TelegramSession.Post(result, Topic);
			pending.Transcript.Add(message.MessageId);
			pending.Transcript.Add(sent.MessageId);
			return;
		}

		await TelegramSession.Delete(message.MessageId);
		TelegramSession.Toast(Topic, result);
		await Show(pending.Query);
	}
}
