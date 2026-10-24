namespace Library.Domain.Common;

/// <summary>
/// Outcome of an operation that can fail for an expected business reason, without using exceptions for control flow.
/// </summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Outcome carrying a <typeparamref name="T"/> value on success or an <see cref="Common.Error"/> on failure.
/// </summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(Error error) : base(error)
    {
    }

    /// <summary>
    /// The success value. Accessing it on a failed result is a programming error and throws.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access the value of a failed result ({Error!.Code}).");

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}
