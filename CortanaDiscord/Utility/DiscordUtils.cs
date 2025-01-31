﻿using CortanaLib;
using CortanaLib.Extensions;
using CortanaLib.Structures;
using Discord;
using Discord.WebSocket;

namespace CortanaDiscord.Utility;

internal static class DiscordUtils
{
	public static readonly DataStruct Data;
	public static readonly Memes Memes;
	public static readonly Guilds GuildSettings;
	public static readonly Dictionary<ulong, DateTime> TimeConnected;
	
	public static DiscordSocketClient Cortana { get; private set; } = null!;

	static DiscordUtils()
	{
		Data = FileHandler.DeserializeJson<DataStruct>(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaDiscord)}/DiscordData.json"));
		Memes = Memes.Load(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaDiscord)}/Memes.json"));
		GuildSettings = Guilds.Load(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaDiscord)}/DiscordGuilds.json"));
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

	public static void UpdateSettings() => GuildSettings.Serialize().Dump(FileHandler.GetPath(EDirType.Config, $"{nameof(CortanaDiscord)}/DiscordGuilds.json"));
	
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
	
	/*
	private static void HardwareSubscription(string message)
	{
		Task.Run(async () => await SendToChannel(message, ECortanaChannels.Log));
	}*/
}

