using Library.Api.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Library.Api.Infrastructure.Caching;

public static class CachingExtensions
{
    private const int ConnectTimeoutMilliseconds = 2000;
    private const int CommandTimeoutMilliseconds = 1000;

    /// <summary>
    /// One shared multiplexer for cache and health checks. It never blocks startup on an unreachable Redis,
    /// uses short timeouts and fails fast instead of queueing commands while disconnected, keeping fail-open reads cheap.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services)
    {
        services.AddOptions<CacheOptions>()
            .BindConfiguration(CacheOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            var options = ConfigurationOptions.Parse(provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value.Redis);
            options.AbortOnConnectFail = false;
            options.ConnectTimeout = ConnectTimeoutMilliseconds;
            options.SyncTimeout = CommandTimeoutMilliseconds;
            options.AsyncTimeout = CommandTimeoutMilliseconds;
            options.BacklogPolicy = BacklogPolicy.FailFast;
            return ConnectionMultiplexer.Connect(options);
        });
        services.AddSingleton<RedisCache>();

        return services;
    }
}
