namespace CortanaLib.Primitives;

public readonly struct Result<T>
{
	private readonly T _value;
	private readonly string _error;

	private Result(T value, string error, bool ok)
	{
		_value = value;
		_error = error;
		IsOk = ok;
	}

	public bool IsOk { get; }

	public T Value => IsOk ? _value : throw new InvalidOperationException($"Result is a failure: {_error}");
	public string Error => IsOk ? "" : _error;

	public static Result<T> Ok(T value) => new(value, "", true);
	public static Result<T> Fail(string error) => new(default!, error, false);

	public TResult Match<TResult>(Func<T, TResult> ok, Func<string, TResult> fail) => IsOk ? ok(_value) : fail(_error);

	public Result<TOther> Map<TOther>(Func<T, TOther> map) => IsOk ? Result<TOther>.Ok(map(_value)) : Result<TOther>.Fail(_error);

	public override string ToString() => IsOk ? _value?.ToString() ?? "" : _error;
}

public static class Result
{
	public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
	public static Result<T> Fail<T>(string error) => Result<T>.Fail(error);

	public static Result<string> Text(string message) => Result<string>.Ok(message);
	public static Result<string> Error(string message) => Result<string>.Fail(message);
}
