using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Processor;
using Timer = Processor.Timer;

namespace DiscordBot.Modules
{
    [Group("timer", "Timers")]
    public class TimerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("timer", "Imposta un timer a cronometro [Default di 5 minuti]")]
        public async Task SetTimer([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("secondi", "Quanti secondi? [Default 0]"), MaxValue(59)] int seconds = 0, [Summary("minuti", "Quanti minuti? [Default 5]"), MaxValue(59)] int minutes = 5, [Summary("ore", "Quante ore? [Default 0]"), MaxValue(100)] int hours = 0, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            var timer = new DiscordTimer(user: Context.User, textChannel: inPrivate == EAnswer.Si ? null : Context.Channel, name: $"{Context.User.Id}:{DateTime.UnixEpoch.Second}", text: text, hours: hours, minutes: minutes, seconds: seconds, callback: TimerFinished, loop: ETimerLoop.No);
            Embed embed = DiscordUtils.CreateEmbed("Timer impostato!", description: $"Per {timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}");
            await RespondAsync(embed: embed);
        }

        [SlashCommand("sveglia", "Imposta una sveglia [Mezzanotte di default]")]
        public async Task SetAlarm([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("ora", "A che ora? [Default 0]"), MaxValue(23)] int hours = 0, [Summary("minuto", "A che minuto? [Default 0]"), MaxValue(59)] int minutes = 0, [Summary("giorno", "Il giorno della settimana [Default oggi]")] DayOfWeek? day = null, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            int dayNumber = DateTime.Now.Day;
            if (day != null)
            {
                if ((int)DateTime.Now.DayOfWeek - (int)day < 0) dayNumber += ((int)day - (int)DateTime.Now.DayOfWeek);
                else dayNumber += 7;
            }
            
            var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dayNumber, hours, minutes, 1);
            var timer = new DiscordTimer(user: Context.User, textChannel: inPrivate == EAnswer.Si ? null : Context.Channel, name: $"{Context.User.Id}:{DateTime.UnixEpoch.Second}", text: text, targetTime: date, callback: TimerFinished, loop: ETimerLoop.No);
            Embed? embed = DiscordUtils.CreateEmbed("Timer impostato!");
            embed = embed.ToEmbedBuilder()
                    .AddField("Notifica", timer.Text ?? "N/A")
                    .AddField("Impostato per", $"{timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}")
                    .Build();
            await RespondAsync(embed: embed);
        }
        
        [SlashCommand("elimina-timer", "Elimina tutti i tuoi timer e sveglie")]
        public async Task ClearTimer()
        {
            foreach (DiscordTimer discordTimer in Timer.GetDiscordTimers().Where(x => Context.User.Equals(x.User)))
            {
                Timer.RemoveTimer(discordTimer);
            }
            Embed embed = DiscordUtils.CreateEmbed("Timer eliminati!");
            await RespondAsync(embed: embed);
        }

        private static async void TimerFinished(object? sender, System.Timers.ElapsedEventArgs args)
        {
            if (sender is not DiscordTimer timer) return;

            try
            {
                var user = (SocketUser) timer.User;
                var textChannel = (SocketTextChannel?) timer.TextChannel;

                Embed embed = DiscordUtils.CreateEmbed("Timer trascorso", user: user);
                embed = embed.ToEmbedBuilder()
                    .AddField("Notifica", timer.Text ?? "N/A")
                    .Build();

                if (textChannel == null) await user.SendMessageAsync(embed: embed);
                else await textChannel.SendMessageAsync(embed: embed);
            }
            catch
            {
                DiscordUtils.SendToChannel("C'è stato un problema con un timer :/", ECortanaChannels.Log);
            }
        }
    }
}
