using System.Security.Cryptography;
using System.Text;
using CortanaKernel.Domain.Common;
using CortanaLib.Primitives;
using CortanaLib.Runtime;
using Microsoft.Extensions.Primitives;

namespace CortanaKernel.Api;

public enum ApiAccess
{
	Public,
	ReadOnly,
	Sensitive
}

public sealed record ApiAccessMetadata(ApiAccess Access);

public static class ApiAccessExtensions
{
	public static TBuilder Access<TBuilder>(this TBuilder builder, ApiAccess access) where TBuilder : IEndpointConventionBuilder
	{
		builder.WithMetadata(new ApiAccessMetadata(access));
		return builder;
	}
}

public sealed class ApiKeyGate
{
	private readonly byte[]? _key;

	private ApiKeyGate(string? configured) => _key = string.IsNullOrEmpty(configured) ? null : Encoding.UTF8.GetBytes(configured);

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
		var gate = new ApiKeyGate(CortanaEnvironment.Read("CORTANA_API_KEY"));

		Log.Write("Api", gate.IsConfigured
			? "An API key is required for every route except / and /health"
			: "CORTANA_API_KEY is not set, so every route except / and /health is disabled");

		return gate;
	}
}

public static class RequestOrigin
{
	private const string Header = "X-Cortana-Surface";

	/// Clients declare which surface they are and the Kernel decides what that is allowed to do
	public static CommandOrigin From(HttpRequest request)
	{
		string value = request.Headers.TryGetValue(Header, out StringValues surface) ? surface.ToString() : "";
		return Enum.TryParse(value, true, out CommandSurface parsed)
			? CommandOrigin.User(parsed)
			: CommandOrigin.User(CommandSurface.Api);
	}
}
