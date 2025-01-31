namespace Utility.Structures;

public readonly struct Result<T, E> {
    private readonly bool _success;
    public readonly T Value;
    public readonly E Error;

    private Result(T v, E e, bool success)
    {
        Value = v;
        Error = e;
        _success = success;
    }

    public bool IsOk => _success;

    public static Result<T, E> Success(T v)
    {
        return new Result<T, E>(v, default(E), true);
    }

    public static Result<T, E> Failure(E e)
    {
        return new Result<T, E>(default(T), e, false);
    }

    public R Match<R>(
        Func<T, R> success,
        Func<E, R> failure) =>
        _success ? success(Value) : failure(Error);
}
