namespace CortanaLib.Structures;

public class CortanaException : Exception
{
	public CortanaException(string message) : base(message)
	{
		DataHandler.Log(nameof(CortanaException), message);
	}
}

public class EnvironmentException(string message) : Exception(message + " not set in the environment");