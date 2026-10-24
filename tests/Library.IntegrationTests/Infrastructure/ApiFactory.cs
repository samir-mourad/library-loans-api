using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

[assembly: AssemblyFixture(typeof(Library.IntegrationTests.Infrastructure.ApiFactory))]

namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the API against real PostgreSQL and Redis containers, started once per test run and shared by all tests.
/// Test-only endpoints are added through a startup filter, keeping production code free of test routes.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
    }

    /// <summary>
    /// Creates a client for an application instance whose connection string points to a closed port.
    /// </summary>
    public WebApplicationFactory<Program> WithUnreachable(string connectionStringKey, string unreachableConnectionString) =>
        WithWebHostBuilder(builder => builder.UseSetting(connectionStringKey, unreachableConnectionString));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
        builder.ConfigureServices(services => services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>());
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }
}
