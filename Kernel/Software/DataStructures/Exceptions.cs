namespace Kernel.Software.DataStructures;

[Serializable]
public class CortanaException : Exception
{
	public CortanaException() { }

	public CortanaException(string message) : base(message)
	{
		FileHandler.Log("CortanaException", message);
	}

	public CortanaException(string message, Exception innerException) : base(message, innerException)
	{
		FileHandler.Log("CortanaException", message);
	}
}