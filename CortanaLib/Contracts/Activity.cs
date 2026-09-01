namespace CortanaLib.Contracts;

public enum ActivityCategory
{
	Idle,
	Browsing,
	Coding,
	Gaming,
	Media,
	Away,
	Locked
}

public enum ActivityDetail
{
	CategoryOnly,
	GameTitles,
	NowPlaying
}

public sealed record NowPlaying(
	string? Artist,
	string? Title,
	string? Album,
	bool Paused);

public sealed record DesktopActivity(
	ActivityCategory Category,
	string? Subject,
	string? Detail,
	DateTimeOffset Since,
	int IdleSeconds,
	bool Locked,
	bool Fullscreen,
	NowPlaying? Playing = null);
