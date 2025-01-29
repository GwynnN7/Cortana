using System.Text.Json;

namespace Kernel.Software.Extensions;

public static class ObjectExtensions
{
    public static string Serialize(this object obj)
    {
        return JsonSerializer.Serialize(obj, FileHandler.SerializerOptions);
    }
}