using Library.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Library.Api.Infrastructure.Idempotency;

/// <summary>
/// Idempotency keys stored in PostgreSQL and claimed inside the caller's transaction, so the key and the business
/// change commit or roll back together. A concurrent request with the same key blocks on the primary key until the
/// first transaction ends, then sees the committed record and replays it; no "in progress" state is ever exposed.
/// </summary>
public sealed class IdempotencyStore(LibraryDbContext dbContext, TimeProvider clock)
{
    public async Task<IdempotencyStart> TryBeginAsync(string scope, string key, string requestHash, CancellationToken cancellationToken)
    {
        EnsureTransaction();

        var claimed = await dbContext.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO idempotency_keys (scope, key, request_hash, created_at)
            VALUES ({scope}, {key}, {requestHash}, {clock.GetUtcNow()})
            ON CONFLICT (scope, key) DO NOTHING
            """,
            cancellationToken);

        if (claimed == 1)
        {
            return IdempotencyStart.Begun;
        }

        var existing = await dbContext.IdempotencyRecords
            .AsNoTracking()
            .SingleAsync(record => record.Scope == scope && record.Key == key, cancellationToken);

        if (existing.RequestHash != requestHash)
        {
            return IdempotencyStart.Mismatch;
        }

        return existing is { StatusCode: { } statusCode, ResponseBody: { } responseBody }
            ? IdempotencyStart.Replay(statusCode, responseBody)
            : throw new InvalidOperationException($"Idempotency key '{key}' was committed without a response.");
    }

    public async Task CompleteAsync(string scope, string key, int statusCode, string responseBody, CancellationToken cancellationToken)
    {
        EnsureTransaction();

        await dbContext.IdempotencyRecords
            .Where(record => record.Scope == scope && record.Key == key)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(record => record.StatusCode, statusCode)
                    .SetProperty(record => record.ResponseBody, responseBody),
                cancellationToken);
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Idempotency keys must be claimed and completed inside the business transaction.");
        }
    }
}
