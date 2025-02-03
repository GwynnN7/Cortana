using CortanaLib.Structures;
using StackExchange.Redis;

namespace CortanaKernel.Kernel;

public static class IpcService
{
    private static readonly ConnectionMultiplexer CommunicationClient;

    static IpcService()
    {
        CommunicationClient = ConnectionMultiplexer.Connect("localhost");
    }

    public static void ShutdownService()
    {
        CommunicationClient.Close();
    }
    
    public static void Publish(EMessageCategory category, string message)
    {
        ISubscriber pub = CommunicationClient.GetSubscriber();
        pub.Publish(RedisChannel.Literal(category.ToString()), message);
    }
}