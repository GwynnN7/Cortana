using Discord.Interactions;
using Discord.WebSocket;
using Utility;

namespace DiscordBot.Modules
{
    [Group("settings", "Impostazioni")]
    public class SettingsModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("canale-saluti", "In che canale volete che vi saluti?")]
        public async Task SetGreetingsChannel([Summary("canale", "Dite il canale")] SocketTextChannel channel)
        {
            DiscordData.GuildSettings[Context.Guild.Id].GreetingsChannel = channel.Id;
            DiscordData.UpdateSettings();
            await RespondAsync($"Da ora in poi vi saluterò in {channel.Name}");
        }

        [SlashCommand("imposta-canale-afk", "Quale è il canale AFK?")]
        public async Task SetAFKChannel([Summary("canale", "Dite il canale")] SocketVoiceChannel channel)
        {
            DiscordData.GuildSettings[Context.Guild.Id].AFKChannel = channel.Id;
            DiscordData.UpdateSettings();
            await RespondAsync($"Canale AFK settato a {channel.Name}");
        }

        [SlashCommand("rimuovi-canale-afk", "Rimuovo il canale AFK")]
        public async Task RemoveAFKChannel()
        {
            DiscordData.GuildSettings[Context.Guild.Id].AFKChannel = 0;
            DiscordData.UpdateSettings();
            await RespondAsync("Canale AFK rimosso");
        }

        [SlashCommand("auto-join", "Volete che entri in automatico?")]
        public async Task SetAutoJoin([Summary("scelta", "Si o No?")] EAnswer answer)
        {
            DiscordData.GuildSettings[Context.Guild.Id].AutoJoin = answer == EAnswer.Si;
            DiscordData.UpdateSettings();
            await RespondAsync(answer == EAnswer.Si ? "Auto-Join attivato" : "Auto-Join disattivato");
        }

        [SlashCommand("greetings", "Volete che vi saluti quando entrate in un canale vocale?")]
        public async Task SetGreetings([Summary("scelta", "Si o No?")] EAnswer answer)
        {
            DiscordData.GuildSettings[Context.Guild.Id].Greetings = answer == EAnswer.Si;
            DiscordData.UpdateSettings();
            await RespondAsync(answer == EAnswer.Si ? "Greetings attivato" : "Greetings disattivato");
        }
    }
}
