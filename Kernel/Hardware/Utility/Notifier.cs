using Kernel.Hardware.DataStructures;

namespace Kernel.Hardware.Utility;

public static class HardwareNotifier
{
    private static readonly List<Tuple<Action<string>, ENotificationPriority>> Subscribers = [];

    public static void Subscribe(Action<string> action, ENotificationPriority priority)
    {
        Subscribers.Add(new Tuple<Action<string>, ENotificationPriority>(action, priority));
    }

    public static void Publish(string message, ENotificationPriority priority)
    {
        foreach (Tuple<Action<string>, ENotificationPriority> subscriber in Subscribers.Where(subscriber => priority >= subscriber.Item2))
        {
            subscriber.Item1.Invoke(message);
        }
    }
}