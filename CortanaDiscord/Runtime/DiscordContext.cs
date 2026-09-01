using System.Collections.Concurrent;
using CortanaLib.Client;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Discord;
using Discord.WebSocket;

namespace CortanaDiscord.Runtime;

public enum Answer
{
	Yes,
	No
}

public enum ListAction
{
	Add,
	Remove
}

public enum CortanaChannel
{
	Cortana,
	Log
}

public sealed record DiscordIdentities
{
	public ulong CortanaId { get; init; }
	public ulong ChiefId { get; init; }
	public ulong HomeId { get; init; }
	public ulong CortanaChannelId { get; init; }
	public ulong LogChannelId { get; init; }
}

public sealed class GuildSettings
{
	public bool Greetings { get; set; }
	public ulong GreetingsChannel { get; set; }
	public List<string> BannedWords { get; init; } = [];
}

public static class DiscordContext
{
	private static readonly string GuildsPath = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaDiscord/Guilds.json");

	public static readonly DiscordIdentities Ids =
		JsonStore.ReadOrNew<DiscordIdentities>(CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaDiscord/Data.json"));

	public static readonly CortanaClient Cortana = CortanaClient.Default.As(CommandSurface.Discord);

	public static readonly ConcurrentDictionary<ulong, DateTimeOffset> VoiceSince = new();

	private static readonly Dictionary<ulong, GuildSettings> Guilds =
		JsonStore.ReadOrNew<Dictionary<ulong, GuildSettings>>(GuildsPath);

	private static readonly Lock Gate = new();

	public static DiscordSocketClient Client { get; private set; } = null!;

	public static void Use(DiscordSocketClient client) => Client = client;

	public static GuildSettings SettingsFor(SocketGuild guild)
	{
		lock (Gate)
		{
			if (Guilds.TryGetValue(guild.Id, out GuildSettings? found)) return found;

			var created = new GuildSettings { GreetingsChannel = guild.DefaultChannel?.Id ?? 0 };
			Guilds[guild.Id] = created;
			JsonStore.Write(GuildsPath, Guilds);
			return created;
		}
	}

	public static GuildSettings? SettingsFor(ulong guildId)
	{
		lock (Gate) return Guilds.GetValueOrDefault(guildId);
	}

	public static void Save()
	{
		lock (Gate) JsonStore.Write(GuildsPath, Guilds);
	}

	public static void Forget(ulong guildId)
	{
		lock (Gate)
		{
			Guilds.Remove(guildId);
			JsonStore.Write(GuildsPath, Guilds);
		}
	}

	public static Embed Card(string title, SocketUser? user = null, string description = "",
		Color? color = null, EmbedFooterBuilder? footer = null, bool timestamp = true, bool anonymous = false)
	{
		user ??= Client.CurrentUser;

		EmbedBuilder builder = new EmbedBuilder()
			.WithTitle(title)
			.WithColor(color ?? Color.Blue)
			.WithDescription(description);

		if (timestamp) builder.WithCurrentTimestamp();
		if (!anonymous) builder.WithAuthor(user.Username, user.GetAvatarUrl());
		if (footer != null) builder.WithFooter(footer);

		return builder.Build();
	}

	public static async Task Post(string text, CortanaChannel channel)
	{
		ulong id = channel == CortanaChannel.Cortana ? Ids.CortanaChannelId : Ids.LogChannelId;

		try
		{
			SocketTextChannel? target = Client.GetGuild(Ids.HomeId)?.GetTextChannel(id);
			if (target != null) await target.SendMessageAsync(text);
		}
		catch (Exception ex)
		{
			Log.Error("Discord", $"Could not post to {channel}: {ex.Message}");
		}
	}

	public static async Task<string> Text(Task<Result<string>> call) => (await call).Match(value => value, error => error);
}
