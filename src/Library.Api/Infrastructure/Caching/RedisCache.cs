using System.Text.Json;
using Library.Api.Infrastructure.Serialization;
using StackExchange.Redis;

namespace Library.Api.Infrastructure.Caching;

/// <summary>
/// Fail-open JSON cache over Redis: any Redis failure is logged and treated as a miss (or a no-op for writes),
/// so a cache outage degrades latency but never fails a request. Redis is never a source of truth.
/// </summary>
public sealed partial class RedisCache(IConnectionMultiplexer redis, ILogger<RedisCache> logger)
{
    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);
            return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value.ToString(), JsonExtensions.StorageSerializerOptions);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFailure(logger, exception, "GET", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan timeToLive)
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonExtensions.StorageSerializerOptions);
            await redis.GetDatabase().StringSetAsync(key, json, timeToLive);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFailure(logger, exception, "SET", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception exception) when (IsRedisFailure(exception))
        {
            LogFailure(logger, exception, "DEL", key);
        }
    }

    private static bool IsRedisFailure(Exception exception) => exception is RedisException or TimeoutException;

    [LoggerMessage(EventId = 3000, Level = LogLevel.Warning, Message = "Redis {Operation} failed for key {CacheKey}; continuing without cache")]
    private static partial void LogFailure(ILogger logger, Exception exception, string operation, string cacheKey);
}
