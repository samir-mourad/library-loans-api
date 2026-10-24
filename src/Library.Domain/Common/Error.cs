namespace Library.Domain.Common;

/// <summary>
/// Expected business failure with a stable machine-readable <paramref name="Code"/> (e.g. <c>book.unavailable</c>)
/// and a human-readable <paramref name="Message"/>.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unprocessable(string code, string message) => new(code, message, ErrorType.Unprocessable);
}
