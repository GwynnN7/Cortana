namespace CortanaLib.Structures;

public readonly struct Result<T, E>
{
    private readonly bool _success;
    private readonly T _value;
    private readonly E _error;

    private Result(T v, E e, bool success)
    {
        _value = v;
        _error = e;
        _success = success;
    }

    public bool IsOk => _success;

    public static Result<T, E> Success(T v)
    {
        return new Result<T, E>(v, default(E)!, true);
    }

    public static Result<T, E> Failure(E e)
    {
        return new Result<T, E>(default(T)!, e, false);
    }

    public R Match<R>(
        Func<T, R> success,
        Func<E, R> failure) =>
        _success ? success(_value) : failure(_error);
}
