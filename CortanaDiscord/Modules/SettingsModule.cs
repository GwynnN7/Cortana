using CortanaDiscord.Utility;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Modules;

[Group("settings", "Impostazioni")]
public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("canale-saluti", "In che canale volete che vi saluti?")]
	public async Task SetGreetingsChannel([Summary("canale", "Dite il canale")] SocketTextChannel channel)
	{
		DiscordUtils.GuildSettings[Context.Guild.Id].GreetingsChannel = channel.Id;
		DiscordUtils.UpdateSettings();
		await RespondAsync($"Da ora in poi vi saluterò in {channel.Name}");
	}

	[SlashCommand("greetings", "Volete che vi saluti quando entrate in un canale vocale?")]
	public async Task SetGreetings([Summary("scelta", "Si o No?")] EAnswer answer)
	{
		DiscordUtils.GuildSettings[Context.Guild.Id].Greetings = answer == EAnswer.Si;
		DiscordUtils.UpdateSettings();
		await RespondAsync(answer == EAnswer.Si ? "Greetings attivato" : "Greetings disattivato");
	}

	[SlashCommand("imposta-canale-afk", "Quale è il canale AFK?")]
	public async Task SetAfkChannel([Summary("canale", "Dite il canale")] SocketVoiceChannel channel)
	{
		DiscordUtils.GuildSettings[Context.Guild.Id].AfkChannel = channel.Id;
		DiscordUtils.UpdateSettings();
		await RespondAsync($"Canale AFK settato a {channel.Name}");
	}

	[SlashCommand("rimuovi-canale-afk", "Rimuovo il canale AFK")]
	public async Task RemoveAfkChannel()
	{
		DiscordUtils.GuildSettings[Context.Guild.Id].AfkChannel = null;
		DiscordUtils.UpdateSettings();
		await RespondAsync("Canale AFK rimosso");
	}

	[SlashCommand("auto-join", "Volete che entri in automatico?")]
	public async Task SetAutoJoin([Summary("scelta", "Si o No?")] EAnswer answer)
	{
		DiscordUtils.GuildSettings[Context.Guild.Id].AutoJoin = answer == EAnswer.Si;
		DiscordUtils.UpdateSettings();
		await RespondAsync(answer == EAnswer.Si ? "Auto-Join attivato" : "Auto-Join disattivato");
	}
}