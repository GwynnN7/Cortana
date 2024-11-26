using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using Processor;

namespace DiscordBot;

public static class DiscordUtils
{
	private const string StoragePath = "Storage/Config/Discord";

	public static readonly DataStruct Data;
	public static readonly Dictionary<string, MemeJsonStructure> Memes;
	public static readonly Dictionary<ulong, GuildSettings> GuildSettings;
	public static readonly Dictionary<ulong, DateTime> TimeConnected;

	static DiscordUtils()
	{
		Data = Software.LoadFile<DataStruct>($"{StoragePath}/DiscordData.json");
		Memes = Software.LoadFile<Dictionary<string, MemeJsonStructure>>($"{StoragePath}/Memes.json") ?? new Dictionary<string, MemeJsonStructure>();
		GuildSettings = Software.LoadFile<Dictionary<ulong, GuildSettings>>($"{StoragePath}/DiscordGuilds.json") ?? new Dictionary<ulong, GuildSettings>();
		TimeConnected = new Dictionary<ulong, DateTime>();
	}

	public static DiscordSocketClient Cortana { get; private set; } = null!;

	public static void InitSettings(DiscordSocketClient client)
	{
		Cortana = client;

		foreach (SocketGuild guild in Cortana.Guilds)
		{
			if (GuildSettings.ContainsKey(guild.Id)) continue;
			AddGuildSettings(guild);
		}
	}

	public static void AddGuildSettings(SocketGuild guild)
	{
		var defaultGuildSettings = new GuildSettings
		(
			false,
			false,
			guild.DefaultChannel.Id,
			null,
			[]
		);
		GuildSettings.Add(guild.Id, defaultGuildSettings);
		UpdateSettings();
	}

	public static void UpdateSettings()
	{
		Software.WriteFile($"{StoragePath}/DiscordGuilds.json", GuildSettings);
	}

	public static Embed CreateEmbed(string title, SocketUser? user = null, string description = "", Color? embedColor = null, EmbedFooterBuilder? footer = null, bool withTimeStamp = true,
		bool withoutAuthor = false)
	{
		Color color = embedColor ?? Color.Blue;
		user ??= Cortana.CurrentUser;

		EmbedBuilder embedBuilder = new EmbedBuilder()
			.WithTitle(title)
			.WithColor(color)
			.WithDescription(description);
		if (withTimeStamp) embedBuilder.WithCurrentTimestamp();
		if (!withoutAuthor) embedBuilder.WithAuthor(user.Username, user.GetAvatarUrl());
		if (footer != null) embedBuilder.WithFooter(footer);
		return embedBuilder.Build();
	}

	public static async void SendToUser(string text, ulong userId)
	{
		IUser? user = await Cortana.GetUserAsync(userId);
		await user.SendMessageAsync(text);
	}

	public static async void SendToUser(Embed embed, ulong userId)
	{
		IUser? user = await Cortana.GetUserAsync(userId);
		await user.SendMessageAsync(embed: embed);
	}

	public static async void SendToChannel(string text, ECortanaChannels channel)
	{
		ulong channelId = channel switch
		{
			ECortanaChannels.Cortana => Data.CortanaChannelId,
			ECortanaChannels.Log => Data.CortanaLogChannelId,
			_ => Data.CortanaLogChannelId
		};
		await Cortana.GetGuild(Data.HomeId).GetTextChannel(channelId).SendMessageAsync(text);
	}

	public static async void SendToChannel(Embed embed, ECortanaChannels channel)
	{
		ulong channelId = channel switch
		{
			ECortanaChannels.Cortana => Data.CortanaChannelId,
			ECortanaChannels.Log => Data.CortanaLogChannelId,
			_ => Data.CortanaLogChannelId
		};
		await Cortana.GetGuild(Data.HomeId).GetTextChannel(channelId).SendMessageAsync(embed: embed);
	}
}

[method: Newtonsoft.Json.JsonConstructor]
public class GuildSettings(
	bool autoJoin,
	bool greetings,
	ulong greetingsChannel,
	ulong? afkChannel,
	List<string> bannedWords)
{
	public bool AutoJoin { get; set; } = autoJoin;
	public bool Greetings { get; set; } = greetings;
	public ulong GreetingsChannel { get; set; } = greetingsChannel;
	public ulong? AfkChannel { get; set; } = afkChannel;
	public List<string> BannedWords { get; } = bannedWords;
}

[method: Newtonsoft.Json.JsonConstructor]
public readonly struct DataStruct(
	ulong cortanaId,
	ulong chiefId,
	ulong noMenId,
	ulong homeId,
	ulong cortanaChannelId,
	ulong cortanaLogChannelId)
{
	public ulong CortanaId { get; } = cortanaId;
	public ulong ChiefId { get; } = chiefId;
	public ulong NoMenId { get; } = noMenId;
	public ulong HomeId { get; } = homeId;
	public ulong CortanaChannelId { get; } = cortanaChannelId;
	public ulong CortanaLogChannelId { get; } = cortanaLogChannelId;
}

[method: Newtonsoft.Json.JsonConstructor]
public readonly struct MemeJsonStructure(
	List<string> alias,
	string link,
	EMemeCategory category)
{
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public List<string> Alias { get; } = alias;

	public string Link { get; } = link;

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public EMemeCategory Category { get; } = category;
}