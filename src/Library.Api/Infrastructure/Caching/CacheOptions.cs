using System.ComponentModel.DataAnnotations;

namespace Library.Api.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Upper bound for how long a book snapshot may stay stale if an invalidation races with a cache fill.
    /// </summary>
    [Range(1, 3600)]
    public int BookTtlSeconds { get; init; } = 60;
}
