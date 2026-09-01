using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Raspberry;

/// Where this Pi is and what it is wired to
public sealed record NetworkProfile
{
	public Location Location { get; init; } = Location.Orvieto;
	public string Gateway { get; init; } = "";
	public string DesktopIp { get; init; } = "";
	public string DesktopMac { get; init; } = "";

	public static NetworkProfile Select(string gateway)
	{
		string path = CortanaEnvironment.Path_(CortanaFolder.Config, "CortanaKernel/Network.json");
		List<NetworkProfile> profiles = JsonStore.Read<List<NetworkProfile>>(path) ?? [];

		if (profiles.Count == 0)
		{
			Log.Write("Network", $"No profiles in '{path}', assuming {Location.Orvieto} with no desktop wiring");
			return new NetworkProfile { Gateway = gateway };
		}

		NetworkProfile? match = profiles.FirstOrDefault(profile => profile.Gateway == gateway);
		if (match != null) return match;

		Log.Write("Network", $"No profile matches gateway {gateway}, falling back to {profiles[0].Location}");
		return profiles[0];
	}
}
