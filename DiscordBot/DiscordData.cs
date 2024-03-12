using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DiscordBot
{
    public static class DiscordData
    {
        private static DiscordSettings LastLoadedSettings;

        public static DiscordSocketClient Cortana;
        public static Dictionary<ulong, DateTime> TimeConnected = new();

        public static DiscordIDs DiscordIDs;
        public static Dictionary<ulong, GuildSettings> GuildSettings = new();

        public static Dictionary<ulong, GamingProfileSet> GamingProfile = new();
        public static IGDBData IGDB;

        public static Dictionary<string, MemeJsonStructure> Memes = new();

        public static void InitSettings(IReadOnlyCollection<SocketGuild> Guilds)
        {
            var discordSettings = Utility.Functions.LoadFile<DiscordSettings>("Data/Discord/GuildConfig.json") ?? new();

            DiscordIDs = discordSettings.IDs;
            if(discordSettings.GuildSettings == null) discordSettings.GuildSettings = GuildSettings;

            foreach (var guild in Guilds)
            {
                if (!discordSettings.GuildSettings.ContainsKey(guild.Id))
                {
                    AddGuildSettings(guild);
                    continue;
                }

                GuildSettings guildSettings = new GuildSettings()
                {
                    AutoJoin = discordSettings.GuildSettings[guild.Id].AutoJoin,
                    GreetingsChannel = discordSettings.GuildSettings[guild.Id].GreetingsChannel,
                    Greetings = discordSettings.GuildSettings[guild.Id].Greetings,
                    AFKChannel = discordSettings.GuildSettings[guild.Id].AFKChannel,
                    BannedWords = discordSettings.GuildSettings[guild.Id].BannedWords
                };
                GuildSettings.Add(guild.Id, guildSettings);
            };
            LastLoadedSettings = discordSettings;
            UpdateSettings();  
        }

        public static void LoadMemes()
        {
            var memesDataResult = Utility.Functions.LoadFile<Dictionary<string, MemeJsonStructure>>("Data/Discord/Memes.json");
            if (memesDataResult != null) Memes = memesDataResult.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        public static void LoadGamingProfiles()
        {
            GamingProfile = Utility.Functions.LoadFile<Dictionary<ulong, GamingProfileSet>>("Data/Discord/GamingProfile.json") ?? new();
        }

        public static void LoadIGDB()
        {
            IGDB = Utility.Functions.LoadFile<IGDBData>("Data/Global/IGDB.json") ?? new();
        }

        public static void AddGuildSettings(SocketGuild guild)
        {
            var defaultGuildSettings = new GuildSettings()
            {
                AutoJoin = false,
                Greetings = false,
                GreetingsChannel = guild.DefaultChannel.Id,
                AFKChannel = 0,
                BannedWords = new List<string>()
            };
            GuildSettings.TryAdd(guild.Id, defaultGuildSettings);
            UpdateSettings();
        }

        public static void UpdateSettings()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            LastLoadedSettings.GuildSettings = GuildSettings;

            Utility.Functions.WriteFile("Data/Discord/GuildConfig.json", LastLoadedSettings, jsonWriteOptions);
        }

        public static void UpdateGamingProfile()
        {
            Utility.Functions.WriteFile("Data/Discord/GamingProfile.json", GamingProfile);
        }

        public static Embed CreateEmbed(string title, SocketUser? user = null, string description = "", Color? embedColor = null, EmbedFooterBuilder? footer = null, bool withTimeStamp = true, bool withoutAuthor = false)
        {
            Color color = (Color)(embedColor == null ? Color.Blue : embedColor);
            if (user == null) user = Cortana.CurrentUser;

            var embedBuilder = new EmbedBuilder()
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
            var user = await Cortana.GetUserAsync(userId);
            await user.SendMessageAsync(text);
        }

        public static async void SendToUser(Embed embed, ulong userId)
        {
            var user = await Cortana.GetUserAsync(userId);
            await user.SendMessageAsync(embed: embed);
        }

        public static async void SendToChannel(string text, ECortanaChannels channel)
        {
            var channelId = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await Cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelId).SendMessageAsync(text);
        }

        public static async void SendToChannel(Embed embed, ECortanaChannels channel)
        {
            var channelId = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await Cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelId).SendMessageAsync(embed: embed);
        }
    }

    public class DiscordSettings
    {
        public DiscordIDs IDs { get; set; }

        public Dictionary<ulong, GuildSettings> GuildSettings { get; set; }
    }

    public class GuildSettings
    {
        public bool AutoJoin { get; set; }
        public bool Greetings { get; set; }
        public ulong GreetingsChannel { get; set; }
        public ulong AFKChannel { get; set; }
        public List<string> BannedWords { get; set; }
    }

    public class DiscordIDs
    {
        public ulong CortanaID { get; set; }
        public ulong ChiefID { get; set; }
        public ulong NoMenID { get; set; }
        public ulong HomeID { get; set; }
        public ulong CortanaChannelID { get; set; }
        public ulong CortanaLogChannelID { get; set; }
    }

    public class IGDBData
    {
        public string ClientID { get; set; }
        public string ClientSecret { get; set; }
    }

    public class GamingProfileSet
    { 
        public string RAWG { get; set; }
        public string Steam { get; set; }
        public string GOG { get; set; }
    }

    public class MemeJsonStructure
    {
        public List<string> Alias { get; set; } = new List<string>();
        public string Link { get; set; }
        public EMemeCategory Category { get; set; }
    }
}