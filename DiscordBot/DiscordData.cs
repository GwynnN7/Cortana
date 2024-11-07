using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Processor;

namespace DiscordBot
{
    public static class DiscordData
    {
        private static DiscordSocketClient _cortana = null!;
        private static DiscordSettings _loadedSettings = null!;
        public static DiscordIDs DiscordIDs { get; private set; } = null!;
        
        public static readonly Dictionary<ulong, DateTime> TimeConnected = new();
        public static readonly Dictionary<ulong, GuildSettings> GuildSettings = new();
        public static readonly Dictionary<string, MemeJsonStructure> Memes = new();
        
        public static void InitSettings(DiscordSocketClient client, IReadOnlyCollection<SocketGuild> guilds)
        {
            DiscordSettings discordSettings = Software.LoadFile<DiscordSettings>("Storage/Config/Discord/DiscordData.json") ?? new DiscordSettings();

            DiscordIDs = discordSettings.IDs;
            _cortana = client;

            foreach (SocketGuild guild in guilds)
            {
                if (!discordSettings.GuildSettings.ContainsKey(guild.Id))
                {
                    AddGuildSettings(guild);
                    continue;
                }

                var guildSettings = new GuildSettings()
                {
                    AutoJoin = discordSettings.GuildSettings[guild.Id].AutoJoin,
                    GreetingsChannel = discordSettings.GuildSettings[guild.Id].GreetingsChannel,
                    Greetings = discordSettings.GuildSettings[guild.Id].Greetings,
                    AFKChannel = discordSettings.GuildSettings[guild.Id].AFKChannel,
                    BannedWords = discordSettings.GuildSettings[guild.Id].BannedWords
                };
                GuildSettings.Add(guild.Id, guildSettings);
            }
            _loadedSettings = discordSettings;
            UpdateSettings();  
        }

        public static void LoadMemes()
        {
            var memesDataResult = Software.LoadFile<Dictionary<string, MemeJsonStructure>>("Storage/Config/Discord/Memes.json");
            memesDataResult?.ToList().ForEach(x => Memes.Add(x.Key, x.Value));
        }

        public static void AddGuildSettings(SocketGuild guild)
        {
            var defaultGuildSettings = new GuildSettings()
            {
                AutoJoin = false,
                Greetings = false,
                GreetingsChannel = guild.DefaultChannel.Id,
                AFKChannel = 0,
                BannedWords = []
            };
            GuildSettings.TryAdd(guild.Id, defaultGuildSettings);
            UpdateSettings();
        }

        public static void UpdateSettings()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            _loadedSettings.GuildSettings = GuildSettings;

            Software.WriteFile("Storage/Config/Discord/DiscordData.json", _loadedSettings, jsonWriteOptions);
        }

        public static Embed CreateEmbed(string title, SocketUser? user = null, string description = "", Color? embedColor = null, EmbedFooterBuilder? footer = null, bool withTimeStamp = true, bool withoutAuthor = false)
        {
            Color color = (embedColor ?? Color.Blue);
            user ??= _cortana.CurrentUser;

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
            IUser? user = await _cortana.GetUserAsync(userId);
            await user.SendMessageAsync(text);
        }

        public static async void SendToUser(Embed embed, ulong userId)
        {
            IUser? user = await _cortana.GetUserAsync(userId);
            await user.SendMessageAsync(embed: embed);
        }

        public static async void SendToChannel(string text, ECortanaChannels channel)
        {
            ulong channelId = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await _cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelId).SendMessageAsync(text);
        }

        public static async void SendToChannel(Embed embed, ECortanaChannels channel)
        {
            ulong channelId = channel switch
            {
                ECortanaChannels.Cortana => DiscordIDs.CortanaChannelID,
                ECortanaChannels.Log => DiscordIDs.CortanaLogChannelID,
                _ => DiscordIDs.CortanaLogChannelID
            };
            await _cortana.GetGuild(DiscordIDs.HomeID).GetTextChannel(channelId).SendMessageAsync(embed: embed);
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

    public class MemeJsonStructure
    {
        public List<string> Alias { get; set; } = new List<string>();
        public string Link { get; set; }
        public EMemeCategory Category { get; set; }
    }
}