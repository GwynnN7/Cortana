using CortanaDiscord.Utility;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Modules;

[Group("settings", "Impostazioni")]
public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("greetings-channel", "Which channel should I greet you in?")]
	public async Task SetGreetingsChannel([Summary("channel", "Pick the channel")] SocketTextChannel channel)
	{
		DiscordUtils.SettingsFor(Context.Guild).GreetingsChannel = channel.Id;
		DiscordUtils.UpdateSettings();
		await RespondAsync($"Da ora in poi vi saluterò in {channel.Name}");
	}

	[SlashCommand("greetings", "Should I greet you when you join a voice channel?")]
	public async Task SetGreetings([Summary("scelta", "Si o No?")] EAnswer answer)
	{
		DiscordUtils.SettingsFor(Context.Guild).Greetings = answer == EAnswer.Yes;
		DiscordUtils.UpdateSettings();
		await RespondAsync(answer == EAnswer.Yes ? "Greetings attivato" : "Greetings disattivato");
	}

	[SlashCommand("set-afk-channel", "Which channel is the AFK one?")]
	public async Task SetAfkChannel([Summary("channel", "Pick the channel")] SocketVoiceChannel channel)
	{
		DiscordUtils.SettingsFor(Context.Guild).AfkChannel = channel.Id;
		DiscordUtils.UpdateSettings();
		await RespondAsync($"AFK channel set to {channel.Name}");
	}

	[SlashCommand("remove-afk-channel", "Remove the AFK channel")]
	public async Task RemoveAfkChannel()
	{
		DiscordUtils.SettingsFor(Context.Guild).AfkChannel = null;
		DiscordUtils.UpdateSettings();
		await RespondAsync("AFK channel removed");
	}

	[SlashCommand("auto-join", "Should I join automatically?")]
	public async Task SetAutoJoin([Summary("scelta", "Si o No?")] EAnswer answer)
	{
		DiscordUtils.SettingsFor(Context.Guild).AutoJoin = answer == EAnswer.Yes;
		DiscordUtils.UpdateSettings();
		await RespondAsync(answer == EAnswer.Yes ? "Auto-Join attivato" : "Auto-Join disattivato");
	}
}
