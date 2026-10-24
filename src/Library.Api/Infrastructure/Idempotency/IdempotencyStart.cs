namespace Library.Api.Infrastructure.Idempotency;

public enum IdempotencyStartKind
{
    Begun,
    Replay,
    Mismatch
}

/// <summary>
/// Outcome of claiming an idempotency key: this request owns it, it must replay a stored response,
/// or the key was used with a different request.
/// </summary>
public sealed record IdempotencyStart(IdempotencyStartKind Kind, int StatusCode = 0, string ResponseBody = "")
{
    public static readonly IdempotencyStart Begun = new(IdempotencyStartKind.Begun);

    public static readonly IdempotencyStart Mismatch = new(IdempotencyStartKind.Mismatch);

    public static IdempotencyStart Replay(int statusCode, string responseBody) =>
        new(IdempotencyStartKind.Replay, statusCode, responseBody);
}
