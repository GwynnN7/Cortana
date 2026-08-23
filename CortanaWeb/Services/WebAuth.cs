using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CortanaWeb.Services;

public sealed class WebAuth
{
	public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
	public const string Policy = "Dashboard";
	public const string LoginPage = "/login";

	private readonly byte[]? _password;

	public WebAuth()
	{
		string? configured = Environment.GetEnvironmentVariable("CORTANA_WEB_PASSWORD");
		_password = string.IsNullOrEmpty(configured) ? null : Encoding.UTF8.GetBytes(configured);
	}

	public bool Enabled => _password != null;

	public bool Validate(string? attempt)
	{
		if (_password == null) return true;
		if (string.IsNullOrEmpty(attempt)) return false;

		return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(attempt), _password);
	}

	public static Task SignIn(HttpContext context)
	{
		var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "Chief")], CookieScheme);
		return context.SignInAsync(CookieScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
	}

	public static Task SignOut(HttpContext context) => context.SignOutAsync(CookieScheme);
}
