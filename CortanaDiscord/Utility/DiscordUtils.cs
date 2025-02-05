global using Memes = System.Collections.Generic.Dictionary<string, CortanaDiscord.Utility.MemeJsonStructure>;
global using Guilds = System.Collections.Generic.Dictionary<ulong, CortanaDiscord.Utility.GuildSettings>;

using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Discord;
using Discord.WebSocket;
using StackExchange.Redis;

namespace CortanaDiscord.Utility;

internal static class DiscordUtils
{
	public static Memes Memes { get; private set; }
	public static readonly DataStruct Data;
	public static readonly Guilds GuildSettings;
	public static readonly Dictionary<ulong, DateTime> TimeConnected;
	private static ConnectionMultiplexer CommunicationClient { get; }
	public static DiscordSocketClient Cortana { get; private set; } = null!;

	static DiscordUtils()
	{
		CommunicationClient = ConnectionMultiplexer.Connect("localhost");
		
		ISubscriber ipc = CommunicationClient.GetSubscriber();
		ipc.Subscribe(RedisChannel.Literal(EMessageCategory.Update.ToString())).OnMessage(async channelMessage => {
			if(channelMessage.Message.HasValue) await SendToChannel(channelMessage.Message.ToString(), ECortanaChannels.Log);
		});
		
		Data = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Data.json").Load<DataStruct>();
		Memes = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Memes.json").Load<Memes>();
		GuildSettings = DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Guilds.json").Load<Guilds>();
		TimeConnected = new Dictionary<ulong, DateTime>();
	}

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
		{
			AutoJoin = false,
			Greetings = false,
			GreetingsChannel = guild.DefaultChannel.Id,
			AfkChannel = null,
			BannedWords = []
		};
		GuildSettings.Add(guild.Id, defaultGuildSettings);
		UpdateSettings();
	}

	public static void UpdateSettings() => GuildSettings.Serialize().Dump(DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Guilds.json"));
	public static void UpdateMemes(Memes newMemes)
	{
		Memes = newMemes;
		Memes.Serialize().Dump(DataHandler.CortanaPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Memes.json"));
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

	public static async Task SendToUser<T>(T data, ulong userId)
	{
		IUser? user = await Cortana.GetUserAsync(userId);
		switch (data)
		{
			case string text:
				await user.SendMessageAsync(text);
				break;
			case Embed embed:
				await user.SendMessageAsync(embed: embed);
				break;
		}
	}

	public static async Task SendToChannel<T>(T data, ECortanaChannels channel)
	{
		ulong channelId = channel switch
		{
			ECortanaChannels.Cortana => Data.CortanaChannelId,
			ECortanaChannels.Log => Data.CortanaLogChannelId,
			_ => Data.CortanaLogChannelId
		};
		switch (data)
		{
			case string text:
				await Cortana.GetGuild(Data.HomeId).GetTextChannel(channelId).SendMessageAsync(text);
				break;
			case Embed embed:
				await Cortana.GetGuild(Data.HomeId).GetTextChannel(channelId).SendMessageAsync(embed: embed);
				break;
		}
	}
}

