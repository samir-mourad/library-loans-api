using Microsoft.EntityFrameworkCore;

namespace Library.Api.Infrastructure.Persistence;

/// <summary>
/// Single source of provider configuration, shared by the application, design-time tooling and tests
/// so all of them produce the same model and SQL.
/// </summary>
public static class LibraryDbContextOptions
{
    /// <summary>
    /// No EF retry strategy on purpose: it conflicts with the explicit transactions used by loan creation.
    /// Transient failures surface to the client, which can safely retry with the same Idempotency-Key.
    /// </summary>
    public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention();
    }
}
