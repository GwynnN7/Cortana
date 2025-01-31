namespace CortanaLib.Structures;

public interface IOption<T>
{
    TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone);
}

public class Some<T>(T data) : IOption<T>
{
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> _) =>
        onSome(data);
}

public class None<T>() : IOption<T>
{
    public TResult Match<TResult>(Func<T, TResult> _, Func<TResult> onNone) =>
        onNone();
}
