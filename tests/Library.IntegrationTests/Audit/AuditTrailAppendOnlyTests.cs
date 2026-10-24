using Library.Api.Features.Audit;
using Library.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Library.IntegrationTests.Audit;

public class AuditTrailAppendOnlyTests(ApiFactory factory)
{
    [Fact]
    public async Task Audit_event_cannot_be_updated()
    {
        var eventId = await InsertAuditEventAsync();
        await using var context = factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"UPDATE audit_events SET actor = 'tampered' WHERE id = {eventId}", TestContext.Current.CancellationToken));

        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Audit_event_cannot_be_deleted()
    {
        var eventId = await InsertAuditEventAsync();
        await using var context = factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"DELETE FROM audit_events WHERE id = {eventId}", TestContext.Current.CancellationToken));

        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Audit_events_cannot_be_truncated()
    {
        await using var context = factory.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlAsync($"TRUNCATE audit_events", TestContext.Current.CancellationToken));

        Assert.Contains("append-only", exception.MessageText, StringComparison.Ordinal);
    }

    private async Task<Guid> InsertAuditEventAsync()
    {
        await using var context = factory.CreateDbContext();
        var auditEvent = new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            EntityType = "Test",
            EntityId = Guid.CreateVersion7(),
            Action = "TestRecorded",
            Actor = "test",
            OccurredAt = DateTimeOffset.UtcNow,
            CorrelationId = "test",
            Data = "{}"
        };

        context.AuditEvents.Add(auditEvent);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return auditEvent.Id;
    }
}
