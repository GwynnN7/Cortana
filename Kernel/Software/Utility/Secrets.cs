using Newtonsoft.Json;

namespace Kernel.Software.Utility;

[method: JsonConstructor]
public readonly struct Secrets(
	string discordToken,
	string telegramToken,
	string desktopPassword,
	string igdbClient,
	string igdbSecret,
	string cortanaPassword)
{
	public string DiscordToken { get; } = discordToken;
	public string TelegramToken { get; } = telegramToken;
	public string DesktopPassword { get; } = desktopPassword;
	public string IgdbClient { get; } = igdbClient;
	public string IgdbSecret { get; } = igdbSecret;
	public string CortanaPassword { get; } = cortanaPassword;
}