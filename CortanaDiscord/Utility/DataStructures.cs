using CortanaLib;

namespace CortanaDiscord.Utility;

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

public readonly struct MemeJsonStructure //: DeserializedObject
{
	public List<string> Alias { get; init; }
	public string Link { get; init;  }
	public EMemeCategory Category { get; init; }
}