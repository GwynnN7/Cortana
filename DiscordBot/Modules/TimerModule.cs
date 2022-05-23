using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    [Group("timer", "Timers")]
    public class TimerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("timer", "Imposta un timer a cronometro [Default di 5 minuti]")]
        public async Task SetTimer([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("secondi", "Quanti secondi? [Default 0]"), MaxValue(59)] int seconds = 0, [Summary("minuti", "Quanti minuti? [Default 5]"), MaxValue(59)] int minutes = 5, [Summary("ore", "Quanti ore? [Default 0]"), MaxValue(100)] int hours = 0, [Summary("loop", "Vuoi il timer in loop?")] ETimerLoop loop = ETimerLoop.No, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            var timer = new Utility.DiscordUserTimer(Guild: Context.Guild, User: Context.User, TextChannel: inPrivate == EAnswer.Si ? null : Context.Channel, Name: $"{Context.User.Id}:{DateTime.UnixEpoch.Second}", Text: text, Hours: hours, Minutes: minutes, Seconds: seconds, Callback: TimerFinished, Loop: loop);
            var embed = DiscordData.CreateEmbed("Timer impostato!", Description: $"Per {timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}");
            await RespondAsync(embed: embed);
        }

        [SlashCommand("sveglia", "Imposta una sveglia [Mezzanotte di default]")]
        public async Task SetAlarm([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("ora", "A che ora? [Default 0]"), MaxValue(23)] int hours = 0, [Summary("minuto", "A che minuto? [Default 0]"), MaxValue(59)] int minutes = 0, [Summary("giorno", "Il giorno della settimana [Default oggi]")] DayOfWeek? day = null, [Summary("loop", "Vuoi il timer in loop?")] ETimerLoop loop = ETimerLoop.No, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            int dayNumber = DateTime.Now.Day;
            if (day != null)
            {
                if ((int)DateTime.Now.DayOfWeek - (int)day < 0) dayNumber += ((int)day - (int)DateTime.Now.DayOfWeek);
                else dayNumber += 7;
            }
            
            var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dayNumber, hours, minutes, 1);
            var timer = new Utility.DiscordUserTimer(Guild: Context.Guild, User: Context.User, TextChannel: inPrivate == EAnswer.Si ? null : Context.Channel, Name: $"{Context.User.Id}:{DateTime.UnixEpoch.Second}", Text: text, TargetTime: date, Callback: TimerFinished, Loop: loop);
            var embed = DiscordData.CreateEmbed("Timer impostato!");
            embed = embed.ToEmbedBuilder()
                    .AddField("Notifica", timer.Text ?? "N/A")
                    .AddField("Impostato per", $"{timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}")
                    .Build();
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

                var embed = DiscordData.CreateEmbed("Timer trascorso", User: user);
                string nextElapsed = timer.LoopType == ETimerLoop.No ? "Nessuna" : $"{timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}";
                embed = embed.ToEmbedBuilder()
                    .AddField("Notifica", timer.Text ?? "N/A")
                    .AddField("Prossimo loop", nextElapsed)
                    .Build();

                if (text_channel == null) await user.SendMessageAsync(embed: embed);
                else await text_channel.SendMessageAsync(embed: embed);
            }
            catch
            {
                var Chief = await DiscordData.Cortana.GetUserAsync(DiscordData.DiscordIDs.ChiefID);
                await Chief.SendMessageAsync("C'è stato un problema con un timer :/");
            }
        }
    }
}
