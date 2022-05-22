using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace DiscordBot.Modules
{
    [Group("timer", "Timers")]
    public class TimerModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("timer", "Imposta un timer a cronometro")]
        public async Task SetTimer([Summary("secondi", "Quanti secondi? [Default 5]"), MaxValue(59)] int seconds = 5, [Summary("minuti", "Quanti minuti? [Default 0]"), MaxValue(59)] int minutes = 0, [Summary("ore", "Quanti ore? [Default 0]"), MaxValue(59)] int hours = 0, [Summary("loop", "Vuoi il timer in loop?")] EAnswer loop = EAnswer.No, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
        {
            await RespondWithModalAsync<TimerModal>("timer");

            [ModalInteraction("timer", true)]
            async Task CodeModalResponse(TimerModal modal)
            {
                var timer = new Utility.DiscordUserTimer(NewGuild: Context.Guild, NewUser: Context.User, OptionalTextChannel: inPrivate == EAnswer.Si ? null : Context.Channel, Name: modal.Name, Text:modal.Text, Hours: hours, Minutes: minutes, Seconds: seconds, Callback: TimerFinished, Loop: loop == EAnswer.Si);
                DateTime timerTargetTime = timer.TimerTargetTime;
                Embed embed = DiscordData.CreateEmbed("Timer impostato!", Description: timerTargetTime.ToString());
                await RespondAsync(embed: embed);
            }
        }
        public class TimerModal : IModal
        {
            public string Title => "Timer";

            [InputLabel("Nome")]
            [ModalTextInput("name", TextInputStyle.Short, placeholder: "Nome del timer...")]
            public string Name { get; set; }

            [InputLabel("Test")]
            [ModalTextInput("text", TextInputStyle.Paragraph, placeholder: "Testo del timer...")]
            public string Text { get; set; }
        }

        private static async void TimerFinished(object? sender, System.Timers.ElapsedEventArgs args)
        {
            if (sender == null) return;

            try
            {
                var timer = (Utility.DiscordUserTimer)sender;

                var user = (SocketUser)timer.User;
                var text_channel = (SocketTextChannel?)timer.TextChannel;

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
