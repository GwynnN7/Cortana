namespace CortanaLib.Structures;

[Serializable]
public class CortanaException : Exception
{
	public CortanaException() { }

	public CortanaException(string message) : base(message)
	{
		DataHandler.Log("CortanaException", message);
	}
}

[Serializable]
public class EnvironmentException : Exception
{
	public EnvironmentException() { }

	public EnvironmentException(string message) : base(message + " not set in the environment")
	{}
}