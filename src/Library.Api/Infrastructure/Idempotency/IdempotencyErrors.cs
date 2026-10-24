using Library.Domain.Common;

namespace Library.Api.Infrastructure.Idempotency;

public static class IdempotencyErrors
{
    public static readonly Error KeyMissing = Error.Validation(
        "idempotency.key_missing",
        $"The {IdempotencyKey.HeaderName} header is required.");

    public static readonly Error KeyInvalid = Error.Validation(
        "idempotency.key_invalid",
        $"The {IdempotencyKey.HeaderName} header must have 1 to {IdempotencyKey.MaxLength} characters among letters, digits, '.', '_', ':' and '-'.");

    public static readonly Error KeyReused = Error.Unprocessable(
        "idempotency.key_reused",
        $"The {IdempotencyKey.HeaderName} was already used with a different request.");
}
