using Discord.Interactions;
using Discord.WebSocket;

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

        [SlashCommand("auto-join", "Volete che sto sempre in un canale vocale?")]
        public async Task SetAutoJoin([Summary("scelta", "Si o No?")] EAnswer answer)
        {
            DiscordData.GuildSettings[Context.Guild.Id].AutoJoin = answer == EAnswer.Si;
            DiscordData.UpdateSettings();
            await RespondAsync("Ok farò come dite");
        }
    }
}
