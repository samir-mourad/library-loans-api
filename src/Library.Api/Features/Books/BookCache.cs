using Library.Api.Infrastructure.Caching;
using Microsoft.Extensions.Options;

namespace Library.Api.Features.Books;

/// <summary>
/// Cache-aside for book snapshots: one key per book serves both the book and its availability, so every change needs
/// a single invalidation. Writers invalidate after commit; the TTL bounds staleness if a fill races an invalidation.
/// Missing books are not cached.
/// </summary>
public sealed class BookCache(RedisCache cache, IOptions<CacheOptions> options)
{
    public static string KeyFor(Guid bookId) => $"library:v1:book:{bookId}";

    public async Task<BookResponse?> GetOrLoadAsync(
        Guid bookId,
        Func<CancellationToken, Task<BookResponse?>> loadFromDatabase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loadFromDatabase);

        var cached = await cache.GetAsync<BookResponse>(KeyFor(bookId));
        if (cached is not null)
        {
            return cached;
        }

        var loaded = await loadFromDatabase(cancellationToken);
        if (loaded is not null)
        {
            await cache.SetAsync(KeyFor(bookId), loaded, TimeSpan.FromSeconds(options.Value.BookTtlSeconds));
        }

        return loaded;
    }

    public Task InvalidateAsync(Guid bookId) => cache.RemoveAsync(KeyFor(bookId));
}
