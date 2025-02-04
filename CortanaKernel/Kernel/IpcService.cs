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

    public static async Task ShutdownService()
    {
        await CommunicationClient.CloseAsync();
    }
    
    public static void Publish(EMessageCategory category, string message)
    {
        try
        {
            ISubscriber pub = CommunicationClient.GetSubscriber();
            pub.Publish(RedisChannel.Literal(category.ToString()), message);
        }
        catch
        {
            //Best-Effort
        }
    }
}