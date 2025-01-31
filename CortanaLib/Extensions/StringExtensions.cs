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
        str = str.ToLower().Capitalize();
        bool enumValid = Enum.TryParse(str, out T value);
        return enumValid ? new Some<T>(value) : new None<T>();
    }

    public static void Dump(this string str, string path)
    {
        File.WriteAllText(path, str);
    }
}