using Library.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Library.IntegrationTests.Persistence;

public class MigrationTests(ApiFactory factory)
{
    [Fact]
    public async Task All_migrations_are_applied()
    {
        await using var context = factory.CreateDbContext();

        var pending = await context.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Empty(pending);
    }

    [Fact]
    public async Task Model_has_no_changes_missing_a_migration()
    {
        await using var context = factory.CreateDbContext();

        var hasPendingChanges = context.Database.HasPendingModelChanges();

        Assert.False(hasPendingChanges);
    }
}
