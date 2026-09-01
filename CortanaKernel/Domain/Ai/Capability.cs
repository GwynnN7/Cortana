using CortanaKernel.Domain.Common;

namespace CortanaKernel.Domain.Ai;

/// Why a capability exists, which is also how tool access is restricted for untrusted surfaces
public enum CapabilityKind
{
	Query,
	Analysis,
	Action,
	Management
}

/// An application capability the AI is allowed to use
public sealed record AiCapability(
	string Name,
	string Description,
	CapabilityKind Kind,
	IReadOnlyList<AiToolParameter> Parameters,
	Func<IReadOnlyDictionary<string, string>, CommandOrigin, CancellationToken, Task<string>> Execute)
{
	public bool IsReadOnly => Kind is CapabilityKind.Query or CapabilityKind.Analysis;

	public AiToolDescriptor Descriptor => new(Name, Description, Parameters);
}

public static class CapabilityArguments
{
	public static string Text(this IReadOnlyDictionary<string, string> arguments, string name, string fallback = "") =>
		arguments.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;

	public static int Integer(this IReadOnlyDictionary<string, string> arguments, string name, int fallback) =>
		int.TryParse(arguments.Text(name), out int value) ? value : fallback;

	public static double Number(this IReadOnlyDictionary<string, string> arguments, string name, double fallback) =>
		double.TryParse(arguments.Text(name), System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : fallback;

	public static bool TryEnum<T>(this IReadOnlyDictionary<string, string> arguments, string name, out T parsed) where T : struct, Enum =>
		Enum.TryParse(arguments.Text(name), ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
}
