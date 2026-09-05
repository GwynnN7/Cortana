using CortanaLib.Contracts;

namespace CortanaLib.Runtime;

/// How a sensor's part in presence reads on a screen
public static class PresenceText
{
	public static string Choice(PresenceRole role) => role switch
	{
		PresenceRole.Reports => "Reports that someone is here",
		PresenceRole.Sustains => "Keeps presence going, never starts it",
		_ => "Nothing to do with presence"
	};

	public static string Badge(PresenceRole role) => role switch
	{
		PresenceRole.Reports => "presence",
		PresenceRole.Sustains => "sustains presence",
		_ => ""
	};
}
