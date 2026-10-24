namespace Library.IntegrationTests.Infrastructure;

/// <summary>
/// Configuration keys and connection strings pointing to a closed local port, used to simulate dependency outages.
/// </summary>
public static class UnreachableDependencies
{
    public const string PostgresKey = "ConnectionStrings:Postgres";
    public const string RedisKey = "ConnectionStrings:Redis";

    public const string Postgres = "Host=127.0.0.1;Port=1;Database=library;Username=library;Password=unused;Timeout=2";
    public const string Redis = "127.0.0.1:1,abortConnect=false,connectTimeout=1000";
}
