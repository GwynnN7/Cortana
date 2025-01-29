namespace Kernel.Software.DataStructures;

public readonly struct Secrets{
	public string DiscordToken { get; init; }
	public string TelegramToken { get; init; }
	public string IgdbClient { get; init; }
	public string IgdbSecret { get; init; } 
	public string CortanaPassword { get; init; }
}