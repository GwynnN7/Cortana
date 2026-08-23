using System.Security.Cryptography;
using System.Text;
using CortanaLib;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.API;

public enum EApiAccess
{
		Public,

		ReadOnly,

		Sensitive
}

public sealed record ApiAccessMetadata(EApiAccess Access);

public static class ApiAccessExtensions
{
	public static TBuilder Access<TBuilder>(this TBuilder builder, EApiAccess access) where TBuilder : IEndpointConventionBuilder
	{
		builder.WithMetadata(new ApiAccessMetadata(access));
		return builder;
	}
}

public sealed class ApiKeyGate
{
	private readonly byte[]? _key;

	public ApiKeyGate(string? configuredKey)
	{
		_key = string.IsNullOrEmpty(configuredKey) ? null : Encoding.UTF8.GetBytes(configuredKey);
	}

	public bool IsConfigured => _key != null;

	public bool Matches(StringValues provided)
	{
		if (_key == null) return false;

		string value = provided.ToString();
		if (string.IsNullOrEmpty(value)) return false;

		return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(value), _key);
	}

	public static ApiKeyGate FromEnvironment()
	{
		var gate = new ApiKeyGate(DataHandler.EnvOrNull("CORTANA_API_KEY"));
		DataHandler.Log(gate.IsConfigured
			? "[API] API key required for every route except / and /health."
			: "[API] CORTANA_API_KEY is not set - hardware and subfunction routes are DISABLED.");
		return gate;
	}
}
