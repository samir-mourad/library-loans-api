using Microsoft.EntityFrameworkCore;

namespace Library.Api.Infrastructure.Persistence;

/// <summary>
/// Unit of work over PostgreSQL, the source of truth for catalog, loans, idempotency and audit data.
/// Entity mappings live in <see cref="IEntityTypeConfiguration{TEntity}"/> classes discovered from this assembly.
/// </summary>
public sealed class LibraryDbContext(DbContextOptions<LibraryDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LibraryDbContext).Assembly);
    }

    /// <summary>
    /// Enums are stored as text so rows stay readable in SQL and in audit investigations.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
