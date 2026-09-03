using CortanaKernel.Domain.Fabric;
using CortanaLib.Contracts;
using CortanaLib.Primitives;
using CortanaLib.Runtime;

namespace CortanaKernel.Infrastructure.Network;

/// Outputs that live on an announced station, switched over the same socket that carries its readings
public sealed class StationChannelWriter(StationSource stations, Fabric fabric) : IChannelWriter
{
	public bool Handles(string source) =>
		fabric.Sources.Any(entry => entry.Id.Equals(source, StringComparison.OrdinalIgnoreCase)
			&& entry.Kind == SourceKind.Station && entry.Outputs.Count > 0);

	public bool Controls(string channel) =>
		fabric.Sources.Any(entry => entry.Kind == SourceKind.Station
			&& entry.Outputs.Contains(channel, StringComparer.OrdinalIgnoreCase));

	public IReadOnlyList<string> Linked(string channel) => [channel];

	public Result<string> Apply(string channel, PowerState state, bool pulse)
	{
		string? source = fabric.Sources
			.FirstOrDefault(entry => entry.Kind == SourceKind.Station
				&& entry.Outputs.Contains(channel, StringComparer.OrdinalIgnoreCase))?.Id;

		return source is null
			? Result.Fail<string>($"No station announces an output called '{channel}'")
			: stations.Command(source, channel, state);
	}
}
