using CortanaDiscord.Utility;
using CortanaLib;
using CortanaLib.Structures;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Timer = CortanaLib.Timer;

namespace CortanaDiscord.Modules;

[Group("timer", "Timers")]
public class TimerModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("timer", "Imposta un timer a cronometro")]
	public async Task SetTimer([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("secondi", "Quanti secondi?")] [MaxValue(59)] int seconds = 0,
		[Summary("minuti", "Quanti minuti?")] [MaxValue(59)]
		int minutes = 0, [Summary("ore", "Quante ore?")] [MaxValue(100)] int hours = 0, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
	{
		var timerArgs = new DiscordTimerPayload<string>(Context.User, inPrivate == EAnswer.No ? Context.Channel : null, text);
		var timer = new Timer($"{Context.User.Id}:{DateTime.UnixEpoch.Second}", timerArgs, DiscordTimerFinished, ETimerType.Discord);
		timer.Set((seconds, minutes, hours));
		
		Embed embed = DiscordUtils.CreateEmbed("Timer impostato!", description: $"Per {timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}");
		await RespondAsync(embed: embed);
	}

	[SlashCommand("sveglia", "Imposta una sveglia [Mezzanotte di default]")]
	public async Task SetAlarm([Summary("notifica", "Cosa vuoi che ti ricordi?")] string text, [Summary("ora", "A che ora?")] [MaxValue(23)] int hours = 0,
		[Summary("minuto", "A che minuto?")] [MaxValue(59)]
		int minutes = 0,
		[Summary("giorno", "Il giorno della settimana")]
		DayOfWeek? day = null, [Summary("privato", "Vuoi che lo mandi in privato?")] EAnswer inPrivate = EAnswer.No)
	{
		int dayNumber = DateTime.Now.Day;
		if (day != null)
		{
			if ((int)DateTime.Now.DayOfWeek - (int)day < 0) dayNumber += (int)day - (int)DateTime.Now.DayOfWeek;
			else dayNumber += 7;
		}

		var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, dayNumber, hours, minutes, 1);
		var timerArgs = new DiscordTimerPayload<string>(Context.User, inPrivate == EAnswer.No ? Context.Channel : null, text);
		var timer = new Timer($"{Context.User.Id}:{DateTime.UnixEpoch.Second}", timerArgs, DiscordTimerFinished, ETimerType.Discord);
		timer.Set(date);
		
		Embed? embed = DiscordUtils.CreateEmbed("Timer impostato!");
		embed = embed.ToEmbedBuilder()
			.AddField("Notifica", text)
			.AddField("Impostato per", $"{timer.NextTargetTime:dddd dd MMMM alle HH:mm:ss}")
			.Build();
		await RespondAsync(embed: embed);
	}

	[SlashCommand("elimina-timer", "Elimina tutti i tuoi timer e sveglie")]
	public async Task ClearTimer()
	{
		foreach (Timer discordTimer in Timer.GetDiscordTimers().Where(x => Context.User.Equals((x.Payload as DiscordTimerPayload<object>)?.User))) Timer.RemoveTimer(discordTimer);
		Embed embed = DiscordUtils.CreateEmbed("Timer eliminati!");
		await RespondAsync(embed: embed);
	}

	private static async Task DiscordTimerFinished(object? sender)
	{
		if (sender is not Timer { TimerType: ETimerType.Discord } timer) return;

		try
		{
			if (timer.Payload is not DiscordTimerPayload<string> payload) return;
			var user = payload.User as SocketUser;
			var textChannel = payload.TextChannel as SocketTextChannel;

			Embed embed = DiscordUtils.CreateEmbed("Timer trascorso", user);
			embed = embed.ToEmbedBuilder()
				.AddField("Notifica", payload.Arg ?? "N/A")
				.Build();

			if (textChannel == null) await user.SendMessageAsync(embed: embed);
			else await textChannel.SendMessageAsync(embed: embed);
		}
		catch(Exception e)
		{
			await DiscordUtils.SendToChannel<string>($"C'è stato un problema con un timer:\n```{e.Message}```", ECortanaChannels.Log);
		}
	}
}