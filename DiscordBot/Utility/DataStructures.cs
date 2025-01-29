using Kernel.Software;
using Kernel.Software.DataStructures;

namespace DiscordBot.Utility;

internal class Guilds : Dictionary<ulong, GuildSettings> //: SerializedObject
{
	public static Guilds Load(string path)
	{
		return FileHandler.DeserializeJson<Guilds>(path) ?? new Guilds();
	}
}

internal class GuildSettings //: SerializedObject
{
	public bool AutoJoin { get; set; }
	public bool Greetings { get; set; }
	public ulong GreetingsChannel { get; set; }
	public ulong? AfkChannel { get; set; }
	public List<string> BannedWords { get; init; } = [];
}

internal readonly struct DataStruct //: DeserializedObject
{
	public ulong CortanaId { get; init; }
	public ulong ChiefId { get; init; }
	public ulong NoMenId { get; init; }
	public ulong HomeId { get; init; }
	public ulong CortanaChannelId { get; init; }
	public ulong CortanaLogChannelId { get; init; }
}

internal class Memes : Dictionary<string, MemeJsonStructure> //: DeserializedObject
{
	public static Memes Load(string path)
	{
		return FileHandler.DeserializeJson<Memes>(path) ?? new Memes();
	}
}


public readonly struct MemeJsonStructure //: DeserializedObject
{
	public List<string> Alias { get; init; }
	public string Link { get; init;  }
	public EMemeCategory Category { get; init; }
}