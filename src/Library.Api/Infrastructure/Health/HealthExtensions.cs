using Library.Api.Infrastructure.Configuration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Library.Api.Infrastructure.Health;

public static class HealthExtensions
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";

    private const string ReadyTag = "ready";
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// PostgreSQL is required to serve traffic (Unhealthy → 503). Redis is a fail-open cache, so its failure only
    /// degrades readiness and keeps the replica in rotation.
    /// </summary>
    public static IServiceCollection AddHealth(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                provider => ConnectionStrings(provider).Postgres,
                name: "postgres",
                tags: [ReadyTag],
                timeout: CheckTimeout)
            .AddRedis(
                provider => provider.GetRequiredService<IConnectionMultiplexer>(),
                name: "redis",
                failureStatus: HealthStatus.Degraded,
                tags: [ReadyTag],
                timeout: CheckTimeout);

        return services;
    }

    /// <summary>
    /// Liveness runs no dependency checks, so an orchestrator never restarts the process because a dependency is down.
    /// </summary>
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        app.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = HealthResponseWriter.WriteAsync
        });

        return app;
    }

    private static ConnectionStringsOptions ConnectionStrings(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<ConnectionStringsOptions>>().Value;
}
