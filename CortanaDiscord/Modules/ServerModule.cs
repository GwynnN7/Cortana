using CortanaDiscord.Runtime;
using Discord.Interactions;
using Discord.WebSocket;

namespace CortanaDiscord.Modules;

[Group("server", "Server moderation")]
public sealed class ServerModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("banned-words", "Show the banned words on this server")]
	public Task ShowBannedWords()
	{
		List<string> words = DiscordContext.SettingsFor(Context.Guild).BannedWords;

		return RespondAsync(words.Count == 0
			? "There are no banned words on this server"
			: "Banned words on this server:\n```\n" + string.Join("\n", words) + "\n```");
	}

	[SlashCommand("banned-words-edit", "Add or remove a banned word")]
	public async Task EditBannedWords([Summary("action", "Add or remove")] ListAction action, [Summary("word", "The word")] string word)
	{
		List<string> words = DiscordContext.SettingsFor(Context.Guild).BannedWords;
		string wanted = word.ToLowerInvariant();

		switch (action)
		{
			case ListAction.Add when words.Contains(wanted):
				await RespondAsync("That word is already banned here");
				return;

			case ListAction.Add:
				words.Add(wanted);
				break;

			case ListAction.Remove when !words.Contains(wanted):
				await RespondAsync("That word is not banned here");
				return;

			case ListAction.Remove:
				words.Remove(wanted);
				break;
		}

		DiscordContext.Save();
		await RespondAsync(action == ListAction.Add ? "Word added" : "Word removed");
	}

	[SlashCommand("kick", "Kick a user")]
	public Task Kick([Summary("user", "Who?")] SocketGuildUser user, [Summary("reason", "Why?")] string reason = "No reason given") =>
		Protected(user, async () =>
		{
			await user.KickAsync(reason);
			await RespondAsync("User kicked");
		});

	[SlashCommand("ban", "Ban a user")]
	public Task Ban([Summary("user", "Who?")] SocketGuildUser user, [Summary("reason", "Why?")] string reason = "No reason given") =>
		Protected(user, async () =>
		{
			await Context.Guild.AddBanAsync(user, reason: reason);
			await RespondAsync("User banned");
		});

	[SlashCommand("timeout", "Time a user out")]
	public Task Timeout([Summary("user", "Who?")] SocketGuildUser user, [Summary("minutes", "How long?")] double minutes = 10) =>
		Protected(user, async () =>
		{
			await user.SetTimeOutAsync(TimeSpan.FromMinutes(minutes));
			await RespondAsync($"User timed out for {minutes} minutes");
		});

	[SlashCommand("timeout-remove", "Lift a user's timeout")]
	public async Task RemoveTimeout([Summary("user", "Who?")] SocketGuildUser user)
	{
		await user.RemoveTimeOutAsync();
		await RespondAsync("Timeout removed");
	}

	private async Task Protected(SocketGuildUser user, Func<Task> action)
	{
		if (user.Id == DiscordContext.Ids.ChiefId)
		{
			await RespondAsync("I would never do such a thing");
			return;
		}

		if (user.Id == DiscordContext.Ids.CortanaId)
		{
			await RespondAsync("Very funny");
			return;
		}

		await action();
	}
}

[Group("settings", "Server settings")]
public sealed class SettingsModule : InteractionModuleBase<SocketInteractionContext>
{
	[SlashCommand("greetings", "Should I greet people when they join a voice channel?")]
	public Task Greetings([Summary("answer", "Yes or no")] Answer answer)
	{
		DiscordContext.SettingsFor(Context.Guild).Greetings = answer == Answer.Yes;
		DiscordContext.Save();

		return RespondAsync(answer == Answer.Yes ? "Greetings enabled" : "Greetings disabled");
	}

	[SlashCommand("greetings-channel", "Where should I greet people?")]
	public Task GreetingsChannel([Summary("channel", "Which channel")] SocketTextChannel channel)
	{
		DiscordContext.SettingsFor(Context.Guild).GreetingsChannel = channel.Id;
		DiscordContext.Save();

		return RespondAsync($"I will greet people in {channel.Name} from now on");
	}
}
