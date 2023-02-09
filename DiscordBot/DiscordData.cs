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
        public static Dictionary<ulong, GuildUsersData> UserGuildData = new Dictionary<ulong, GuildUsersData>();

        public static Dictionary<string, MemeJsonStructure> Memes = new Dictionary<string, MemeJsonStructure>();
        public static Dictionary<ulong, Projects> Projects = new Dictionary<ulong, Projects>();

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

        static public void LoadData(IReadOnlyCollection<SocketGuild> Guilds)
        {
            Dictionary<ulong, GuildUsersData>? userDataResult = null;
            if(File.Exists("Data/Discord/GuildUserData.json"))
            {
                var file = File.ReadAllText("Data/Discord/GuildUserData.json");

                userDataResult = JsonConvert.DeserializeObject<Dictionary<ulong, GuildUsersData>>(file);
            }
            if (userDataResult == null) userDataResult = new Dictionary<ulong, GuildUsersData>();

            foreach (var Guild in Guilds)
            {
                if (!userDataResult.ContainsKey(Guild.Id))
                {
                    AddGuildUserData(Guild);
                    continue;
                }
                GuildUsersData usersData = new GuildUsersData();
                foreach(var User in Guild.Users)
                {
                    if (User.IsBot) continue;
                    if(!userDataResult[Guild.Id].UserData.ContainsKey(User.Id))
                    {
                        usersData.UserData.Add(User.Id, new GuildUserData());
                        continue;
                    }
                    usersData.UserData.Add(User.Id, userDataResult[Guild.Id].UserData[User.Id]);
                }
                UserGuildData.Add(Guild.Id, usersData);
            }
            UpdateUserGuildData();
        }

        static public void LoadProjects(IReadOnlyCollection<SocketGuild> Guilds)
        {
            Dictionary<ulong, Projects>? projectsResult = null;
            if (File.Exists("Data/Discord/Projects.json"))
            {
                var file = File.ReadAllText("Data/Discord/Projects.json");

                projectsResult = JsonConvert.DeserializeObject<Dictionary<ulong, Projects>>(file);
            }
            if (projectsResult == null) projectsResult = new Dictionary<ulong, Projects>();

            foreach (var Guild in Guilds)
            {
                foreach (var User in Guild.Users)
                {
                    if (User.IsBot) continue;
                    if (!projectsResult.ContainsKey(User.Id))
                    {
                        projectsResult.Add(User.Id, new Projects());
                        continue;
                    }
                }
            }
            Projects = projectsResult;
            UpdateProjects();
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

        static public void AddGuildUserData(SocketGuild Guild)
        {
            var userData = new GuildUsersData()
            {
                UserData = Guild.Users.Where(x => !x.IsBot).ToDictionary(x => x.Id, x => new GuildUserData())
            };
            UserGuildData.Add(Guild.Id, userData);
            UpdateUserGuildData();
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

        static public void UpdateUserGuildData()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            var newJson = JsonConvert.SerializeObject(UserGuildData, jsonWriteOptions);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/GuildUserData.json");
            File.WriteAllText(filePath, newJson);
        }

        static public void UpdateProjects()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            var newJson = JsonConvert.SerializeObject(Projects, jsonWriteOptions);

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/Projects.json");
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
    }

    public class DiscordIDs
    {
        public ulong CortanaID { get; set; }
        public ulong ChiefID { get; set; }
        public ulong NoMenID { get; set; }
        public ulong HomeID { get; set; }
        public ulong CortanaChannelID { get; set; }
        public ulong CortanaLogChannelID { get; set; }
        public ulong GulagID { get; set; }
    }
}

public class GuildUsersData
{
    public Dictionary<ulong, GuildUserData> UserData { get; set; } = new Dictionary<ulong, GuildUserData>();
}

public class GuildUserData
{
    public Statistics Stats { get; set; } = new Statistics();
}

public class Statistics
{
    public ulong TimeConnected { get; set; } = 0;
    public ulong MessagesSent { get; set; } = 0;
    public int QuizPlayed { get; set; } = 0;
    public int QuizWon { get; set; } = 0;
    public int MemesPlayed { get; set; } = 0;
    public int MemesAdded { get; set; } = 0;
    public int SongPlayed { get; set; } = 0;
    public List<string> Projects { get; set; } = new List<string>();
    public string Image { get; set; } = "";
}

public class MemeJsonStructure
{
    public List<string> Alias { get; set; } = new List<string>();
    public string Link { get; set; }
    public EMemeCategory Category { get; set; }
}