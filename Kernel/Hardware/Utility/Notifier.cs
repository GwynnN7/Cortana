namespace Kernel.Hardware.Utility;

public static class HardwareNotifier
{
    private static readonly List<Action<string>> Subscribers = [];

    public static void Subscribe(Action<string> action)
    {
        Subscribers.Add(action);
    }

    public static void Publish(string message)
    {
        foreach (Action<string> subscriber in Subscribers)
        {
            subscriber.Invoke(message);
        }
    }
}