using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Data;

namespace DiscordBot
{
    public enum EAnswer
    {
        Si,
        No
    }

    public enum Action
    {
        Crea,
        Elimina
    }

    public static class DiscordData
    {
        private static DiscordSettings LastLoadedSettings;

        public static SocketSelfUser? CortanaUser;
        public static Dictionary<ulong, DateTime> TimeConnected = new Dictionary<ulong, DateTime>();

        public static DiscordIDs DiscordIDs;
        public static Dictionary<ulong, GuildSettings> GuildSettings = new Dictionary<ulong, GuildSettings>();
        public static Dictionary<ulong, GuildUsersData> UserGuildData = new Dictionary<ulong, GuildUsersData>();

        static public void InitSettings(IReadOnlyCollection<SocketGuild> Guilds)
        {
            DiscordSettings? discordSettings = null;
            if (File.Exists("Data/Discord/GuildConfig.json"))
            {
                Console.WriteLine(Directory.GetCurrentDirectory());
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
                Console.WriteLine(Directory.GetCurrentDirectory());
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

        static public void AddGuildSettings(SocketGuild Guild)
        {
            var DefaultGuildSettings = new GuildSettings()
            {
                AutoJoin = false,
                GreetingsChannel = Guild.DefaultChannel.Id,
            };
            if (!GuildSettings.ContainsKey(Guild.Id)) GuildSettings.Add(Guild.Id, DefaultGuildSettings);
        }

        static public void AddGuildUserData(SocketGuild Guild)
        {
            var userData = new GuildUsersData()
            {
                UserData = Guild.Users.Where(x => !x.IsBot).ToDictionary(x => x.Id, x => new GuildUserData())
            };
            UserGuildData.Add(Guild.Id, userData);
        }

        static public void UpdateSettings()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            LastLoadedSettings.GuildSettings = GuildSettings;
            var newJson = JsonConvert.SerializeObject(LastLoadedSettings, jsonWriteOptions);

            var fileSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/GuildConfig.json");
            File.WriteAllText(fileSettingsPath, newJson);
        }

        static public void UpdateUserGuildData()
        {
            var jsonWriteOptions = new JsonSerializerSettings() { Formatting = Formatting.Indented };
            jsonWriteOptions.Converters.Add(new StringEnumConverter());

            var newJson = JsonConvert.SerializeObject(UserGuildData, jsonWriteOptions);

            var fileSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "Data/Discord/GuildUserData.json");
            File.WriteAllText(fileSettingsPath, newJson);
        }

        static public Embed CreateEmbed(string Title, SocketUser? User = null, string Description = "", Color? EmbedColor = null, EmbedFooterBuilder? Footer = null, bool WithTimeStamp = true, bool WithoutAuthor = false)
        {
            Color Color = (Color)(EmbedColor == null ? Color.Blue : EmbedColor);
            if (User == null) User = CortanaUser;

            var EmbedBuilder = new EmbedBuilder()
                .WithTitle(Title)
                .WithColor(Color)
                .WithDescription(Description);
            if (WithTimeStamp) EmbedBuilder.WithCurrentTimestamp();
            if (!WithoutAuthor) EmbedBuilder.WithAuthor(User.Username, User.GetAvatarUrl());
            if (Footer != null) EmbedBuilder.WithFooter(Footer);
            return EmbedBuilder.Build();
        }

        static public string GetRandomGreetings()
        {
            var choices = new string[]
            {
                "Hi",
                "Hello",
                "Welcome",
                "Good to see you"
            };
            return RequestsHandler.Functions.RandomOption(choices);
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
        public ulong CortanaChannelID { get; set; }
        public ulong NoMenID { get; set; }
        public ulong HomeID { get; set; }
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