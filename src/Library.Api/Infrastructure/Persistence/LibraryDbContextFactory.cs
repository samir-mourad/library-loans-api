using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Library.Api.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> and the migration bundle, so they do not boot the web host
/// (which validates Redis and other runtime options). The connection string comes from
/// <c>ConnectionStrings__Postgres</c>; a credential-less local default is enough to scaffold migrations.
/// </summary>
internal sealed class LibraryDbContextFactory : IDesignTimeDbContextFactory<LibraryDbContext>
{
    private const string ConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string LocalDefault = "Host=localhost;Port=5432;Database=library";

    public LibraryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable) ?? LocalDefault;
        var options = new DbContextOptionsBuilder<LibraryDbContext>();

        LibraryDbContextOptions.Configure(options, connectionString);

        return new LibraryDbContext(options.Options);
    }
}
