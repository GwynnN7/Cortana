using CortanaLib.Structures;
using StackExchange.Redis;

namespace CortanaKernel.Hardware.Utility;

public static class NotificationHandler
{
    private static readonly ConnectionMultiplexer CommunicationClient;

    static NotificationHandler()
    {
        CommunicationClient = ConnectionMultiplexer.Connect("localhost");
    }

    public static void Stop()
    {
        CommunicationClient.Close();
    }
    
    public static void Publish(EMessageCategory category, string message)
    {
        ISubscriber pub = CommunicationClient.GetSubscriber();
        pub.Publish(RedisChannel.Literal(category.ToString()), message);
    }
}