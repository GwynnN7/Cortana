namespace Kernel.Software.Extensions;

public static class StringExtensions
{
    public static string Capitalize(this string str)
    {
        return string.Concat(str[..1].ToUpper(), str.AsSpan(1));
    }
    
    public static T? ToEnum<T>(this string str) where T : struct
    {
        str = str.ToLower().Capitalize();
        bool res = Enum.TryParse(str, out T status);
        return res ? status : null;
    }

    public static void Dump(this string str, string file, string? path = null)
    {
        if(path != null) file = Path.Combine(path, file);
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), file);
        File.WriteAllText(filePath, str);
    }
}