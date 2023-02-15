using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Data;

namespace DiscordBot
{
    public static class DiscordData
    {
        private static DiscordSettings LastLoadedSettings;

        public static DiscordSocketClient Cortana;
        public static Dictionary<ulong, DateTime> TimeConnected = new Dictionary<ulong, DateTime>();

        public static DiscordIDs DiscordIDs;
        public static Dictionary<ulong, GuildSettings> GuildSettings = new Dictionary<ulong, GuildSettings>();

        public static Dictionary<ulong, GamingProfileSet> GamingProfile = new Dictionary<ulong, GamingProfileSet>();
        public static IGDBData IGDB;

        public static Dictionary<string, MemeJsonStructure> Memes = new Dictionary<string, MemeJsonStructure>();

        static public void InitSettings(IReadOnlyCollection<SocketGuild> Guilds)
        {
            DiscordSettings? discordSettings = null;
            if (File.Exists("Data/Discord/GuildConfig.json"))
            {
                var file = File.ReadAllText("Data/Discord/GuildConfig.json");

                discordSettings = JsonConvert.DeserializeObject<DiscordSettings>(file);
            }
            if (discordSettings == null) discordSettings = new DiscordSettings();

            DiscordIDs = discordSettings.IDs;
            if (discordSettings.GuildSettings == null) discordSettings.GuildSettings = GuildSettings;

            foreach (var Guild in Guilds)
            {
                if (!discordSettings.GuildSettings.ContainsKey(Guild.Id))
                {
                    AddGuildSettings(Guild);
                    continue;
                }

                GuildSettings guildSettings = new GuildSettings()
                {
                    AutoJoin = discordSettings.GuildSettings[Guild.Id].AutoJoin,
                    GreetingsChannel = discordSettings.GuildSettings[Guild.Id].GreetingsChannel
                };
                GuildSettings.Add(Guild.Id, guildSettings);
            };
            LastLoadedSettings = discordSettings;
            UpdateSettings();  
        }

        static public void LoadMemes()
        {
            Dictionary<string, MemeJsonStructure>? memesDataResult = null;
            if (File.Exists("Data/Discord/Memes.json"))
            {
                var file = File.ReadAllText("Data/Discord/Memes.json");

                memesDataResult = JsonConvert.DeserializeObject<Dictionary<string, MemeJsonStructure>>(file);
            }
            if (memesDataResult != null) Memes = memesDataResult.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        static public void LoadGamingProfiles()
        {
            Dictionary<ulong, GamingProfileSet>? gamingProfilesResult = null;
            if (File.Exists("Data/Discord/GamingProfile.json"))
            {
                var file = File.ReadAllText("Data/Discord/GamingProfile.json");

                gamingProfilesResult = JsonConvert.DeserializeObject<Dictionary<ulong, GamingProfileSet>>(file);
            }
            if (gamingProfilesResult != null) GamingProfile = gamingProfilesResult;
        }

        static public void LoadIGDB()
        {
            IGDBData? IGDBResult = null;
            if (File.Exists("Data/Global/IGDB.json"))
            {
                var file = File.ReadAllText("Data/Global/IGDB.json");

                IGDBResult = JsonConvert.DeserializeObject<IGDBData>(file);
            }
            if (IGDBResult != null) IGDB = IGDBResult;
        }

        static public void AddGuildSettings(SocketGuild Guild)
        {
            var DefaultGuildSettings = new GuildSettings()
            {
                AutoJoin = false,
                GreetingsChannel = Guild.DefaultChannel.Id,
            };
            if (!GuildSettings.ContainsKey(Guild.Id)) GuildSettings.Add(Guild.Id, DefaultGuildSettings);
            UpdateSettings();
        }

        static public void UpdateSettings()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            LastLoadedSettings.GuildSettings = GuildSettings;
            var newJson = JsonConvert.SerializeObject(LastLoadedSettings, jsonWriteOptions);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/GuildConfig.json");
            File.WriteAllText(filePath, newJson);
        }

        static public void UpdateGamingProfile()
        {
            var newJson = JsonConvert.SerializeObject(GamingProfile);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/GamingProfile.json");
            File.WriteAllText(filePath, newJson);
        }

        static public Embed CreateEmbed(string Title, SocketUser? User = null, string Description = "", Color? EmbedColor = null, EmbedFooterBuilder? Footer = null, bool WithTimeStamp = true, bool WithoutAuthor = false)
        {
            Color Color = (Color)(EmbedColor == null ? Color.Blue : EmbedColor);
            if (User == null) User = Cortana.CurrentUser;

            var EmbedBuilder = new EmbedBuilder()
                .WithTitle(Title)
                .WithColor(Color)
                .WithDescription(Description);
            if (WithTimeStamp) EmbedBuilder.WithCurrentTimestamp();
            if (!WithoutAuthor) EmbedBuilder.WithAuthor(User.Username, User.GetAvatarUrl());
            if (Footer != null) EmbedBuilder.WithFooter(Footer);
            return EmbedBuilder.Build();
        }

        static public async void SendToUser(string text, ulong user_id)
        {
            var user = await Cortana.GetUserAsync(user_id);
            await user.SendMessageAsync(text);
        }

        static public async void SendToUser(Embed embed, ulong user_id)
        {
            var user = await Cortana.GetUserAsync(user_id);
            await user.SendMessageAsync(embed: embed);
        }

        static public async void SendToChannel(string text, ECortanaChannels channel)
        {
            var channelid = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await Cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelid).SendMessageAsync(text);
        }

        static public async void SendToChannel(Embed embed, ECortanaChannels channel)
        {
            var channelid = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await Cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelid).SendMessageAsync(embed: embed);
        }
    }

    public class DiscordSettings
    {
        public string Token { get; set; }
        public DiscordIDs IDs { get; set; }

        public Dictionary<ulong, GuildSettings> GuildSettings { get; set; }
    }

    public class GuildSettings
    {
        public bool AutoJoin { get; set; }
        public ulong GreetingsChannel { get; set; }
        public ulong AFKChannel { get; set; }
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
        public string RAWG { get; set; }  //https://rawg.io/@username
        public string Steam { get; set; } //https://steamcommunity.com/id/username/
        public string GOG { get; set; } //https://www.gog.com/u/username
    }

    public class MemeJsonStructure
    {
        public List<string> Alias { get; set; } = new List<string>();
        public string Link { get; set; }
        public EMemeCategory Category { get; set; }
    }
}