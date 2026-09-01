using CortanaLib.Runtime;

namespace CortanaTelegram.Runtime;

public sealed record TelegramConfig
{
	public long HomeGroup { get; init; }
	public Topics Topics { get; init; } = new();
	public Dictionary<long, string> Usernames { get; init; } = [];

	public static TelegramConfig Load() =>
		JsonStore.ReadOrNew<TelegramConfig>(CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaTelegram/Data.json"));

	public long? IdOf(string username)
	{
		foreach ((long id, string name) in Usernames)
			if (name.Equals(username, StringComparison.OrdinalIgnoreCase)) return id;

		return null;
	}
}

public sealed record Topics
{
	public int Home { get; init; }
	public int Devices { get; init; }
	public int Sensors { get; init; }
	public int System { get; init; }
	public int Cortana { get; init; }
	public int Log { get; init; }
}
