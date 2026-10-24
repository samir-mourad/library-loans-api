using Library.Domain.Common;

namespace Library.Api.Infrastructure.Idempotency;

/// <summary>
/// Result of an idempotent operation, flagging whether it was replayed from a previously stored response.
/// </summary>
public sealed record IdempotentOutcome<T>(Result<T> Result, bool IsReplay)
{
    public static IdempotentOutcome<T> Fresh(Result<T> result) => new(result, false);

    public static IdempotentOutcome<T> Replayed(Result<T> result) => new(result, true);
}
