using System.Text.Json.Serialization;

namespace DiscordBot.Utility;

[method: Newtonsoft.Json.JsonConstructor]
internal class GuildSettings(
	bool autoJoin,
	bool greetings,
	ulong greetingsChannel,
	ulong? afkChannel,
	List<string> bannedWords)
{
	public bool AutoJoin { get; set; } = autoJoin;
	public bool Greetings { get; set; } = greetings;
	public ulong GreetingsChannel { get; set; } = greetingsChannel;
	public ulong? AfkChannel { get; set; } = afkChannel;
	public List<string> BannedWords { get; } = bannedWords;
}

[method: Newtonsoft.Json.JsonConstructor]
internal readonly struct DataStruct(
	ulong cortanaId,
	ulong chiefId,
	ulong noMenId,
	ulong homeId,
	ulong cortanaChannelId,
	ulong cortanaLogChannelId)
{
	public ulong CortanaId { get; } = cortanaId;
	public ulong ChiefId { get; } = chiefId;
	public ulong NoMenId { get; } = noMenId;
	public ulong HomeId { get; } = homeId;
	public ulong CortanaChannelId { get; } = cortanaChannelId;
	public ulong CortanaLogChannelId { get; } = cortanaLogChannelId;
}

[method: Newtonsoft.Json.JsonConstructor]
internal readonly struct MemeJsonStructure(
	List<string> alias,
	string link,
	EMemeCategory category)
{
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public List<string> Alias { get; } = alias;

	public string Link { get; } = link;

	[JsonConverter(typeof(JsonStringEnumConverter))]
	public EMemeCategory Category { get; } = category;
}