using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    [Group("timer", "Timers")]
    public class TimerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("timer", "Imposta un timer a cronometro [Default di 5 minuti]")]
        public async Task SetTimer([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("secondi", "Quanti secondi? [Default 0]"), MaxValue(59)] int seconds = 0, [Summary("minuti", "Quanti minuti? [Default 5]"), MaxValue(59)] int minutes = 5, [Summary("ore", "Quanti ore? [Default 0]"), MaxValue(59)] int hours = 0, [Summary("loop", "Vuoi il timer in loop?")] EAnswer loop = EAnswer.No, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            var timer = new Utility.DiscordUserTimer(Guild: Context.Guild, User: Context.User, TextChannel: inPrivate == EAnswer.Si ? null : Context.Channel, Name: $"{Context.User.Id}:{DateTime.UnixEpoch.Second}", Text: text, Hours: hours, Minutes: minutes, Seconds: seconds, Callback: TimerFinished, Loop: loop == EAnswer.Si);
            var embed = DiscordData.CreateEmbed("Timer impostato!", Description: $"Per: {timer.NextTargetTime:dddd, dd MMMM HH:mm:ss}, tra {timer.NextDeltaTime}");
            await RespondAsync(embed: embed);
        }

        private static async void TimerFinished(object? sender, System.Timers.ElapsedEventArgs args)
        {
            if (sender == null) return;
            if (sender is not Utility.DiscordUserTimer) return;

            try
            {
                var timer = (Utility.DiscordUserTimer) sender;

                var user = (SocketUser) timer.User;
                var text_channel = (SocketTextChannel?) timer.TextChannel;

                if (text_channel == null) await user.SendMessageAsync($"Timer: {timer.Text}");
                else await text_channel.SendMessageAsync($"Timer: {timer.Text}");
            }
            catch
            {
                var Chief = await DiscordData.Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync("C'è stato un problema con un timer :/");
            }
        }
    }
}
