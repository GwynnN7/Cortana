using Discord;
using Discord.WebSocket;
using Kernel.Hardware;
using Kernel.Hardware.DataStructures;
using Kernel.Software;

namespace DiscordBot.Utility;

internal static class DiscordUtils
{
	private static readonly string StoragePath;

	public static readonly DataStruct Data;
	public static readonly Dictionary<string, MemeJsonStructure> Memes;
	public static readonly Dictionary<ulong, GuildSettings> GuildSettings;
	public static readonly Dictionary<ulong, DateTime> TimeConnected;
	
	public static DiscordSocketClient Cortana { get; private set; } = null!;

	static DiscordUtils()
	{
		StoragePath = Path.Combine(FileHandler.ProjectStoragePath, "Config/Discord/");
		
		Data = FileHandler.LoadFile<DataStruct>(Path.Combine(StoragePath, "DiscordData.json"));
		Memes = FileHandler.LoadFile<Dictionary<string, MemeJsonStructure>>(Path.Combine(StoragePath, "Memes.json")) ?? new Dictionary<string, MemeJsonStructure>();
		GuildSettings = FileHandler.LoadFile<Dictionary<ulong, GuildSettings>>(Path.Combine(StoragePath, "DiscordGuilds.json")) ?? new Dictionary<ulong, GuildSettings>();
		TimeConnected = new Dictionary<ulong, DateTime>();
		
		HardwareAdapter.SubscribeNotification(HardwareSubscription, ENotificationPriority.Low);
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
		FileHandler.WriteFile(Path.Combine(StoragePath, "DiscordGuilds.json"), GuildSettings);
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

	private static void HardwareSubscription(string message)
	{
		Task.Run(async () => await SendToChannel(message, ECortanaChannels.Log));
	}
}

