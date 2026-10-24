using System.Net;
using Library.Api.Infrastructure.Health;
using Library.IntegrationTests.Infrastructure;

namespace Library.IntegrationTests.Health;

public class HealthTests(ApiFactory factory)
{
    [Fact]
    public async Task Live_returns_200_when_postgres_is_unreachable()
    {
        await using var app = factory.WithUnreachable(UnreachableDependencies.PostgresKey, UnreachableDependencies.Postgres);
        using var client = app.CreateClient();

        var response = await client.SendGetAsync(HealthExtensions.LivePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_returns_200_when_all_dependencies_are_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.SendGetAsync(HealthExtensions.ReadyPath);
        var checks = await ReadCheckStatusesAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", checks["postgres"]);
        Assert.Equal("Healthy", checks["redis"]);
    }

    [Fact]
    public async Task Ready_returns_503_when_postgres_is_unreachable()
    {
        await using var app = factory.WithUnreachable(UnreachableDependencies.PostgresKey, UnreachableDependencies.Postgres);
        using var client = app.CreateClient();

        var response = await client.SendGetAsync(HealthExtensions.ReadyPath);
        var checks = await ReadCheckStatusesAsync(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", checks["postgres"]);
    }

    [Fact]
    public async Task Ready_stays_200_with_redis_degraded_when_redis_is_unreachable()
    {
        await using var app = factory.WithUnreachable(UnreachableDependencies.RedisKey, UnreachableDependencies.Redis);
        using var client = app.CreateClient();

        var response = await client.SendGetAsync(HealthExtensions.ReadyPath);
        var checks = await ReadCheckStatusesAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Degraded", checks["redis"]);
        Assert.Equal("Healthy", checks["postgres"]);
    }

    private static async Task<Dictionary<string, string>> ReadCheckStatusesAsync(HttpResponseMessage response)
    {
        var body = await response.ReadJsonAsync();

        return body.GetProperty("checks")
            .EnumerateArray()
            .ToDictionary(
                check => check.GetProperty("name").GetString()!,
                check => check.GetProperty("status").GetString()!);
    }
}
