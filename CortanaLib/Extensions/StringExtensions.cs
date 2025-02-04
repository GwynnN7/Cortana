using CortanaLib.Structures;

namespace CortanaLib.Extensions;

public static class StringExtensions
{
    public static string Capitalize(this string str)
    {
        return string.Concat(str[..1].ToUpper(), str.AsSpan(1));
    }
    
    public static IOption<T> ToEnum<T>(this string str) where T : struct
    {
        try
        {
            var value = Enum.Parse<T>(str, true);
            return new Some<T>(value);
        }
        catch
        {
            return new None<T>();
        }
    }

    public static void Dump(this string str, string path)
    {
        File.WriteAllText(path, str);
    }
}