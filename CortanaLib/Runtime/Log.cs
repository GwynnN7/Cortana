namespace CortanaLib.Runtime;

/// Console logging only
public static class Log
{
	public static string Write(string scope, string message)
	{
		Console.WriteLine($"[{scope}] {message}");
		return message;
	}

	public static void Error(string scope, string message) => Console.Error.WriteLine($"[{scope}] {message}");
}
