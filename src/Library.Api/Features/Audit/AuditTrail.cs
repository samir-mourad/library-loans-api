using System.Text.Json;
using Library.Api.Infrastructure.Actor;
using Library.Api.Infrastructure.Correlation;
using Library.Api.Infrastructure.Persistence;
using Library.Api.Infrastructure.Serialization;

namespace Library.Api.Features.Audit;

/// <summary>
/// Adds audit events to the request's <see cref="LibraryDbContext"/>, stamping actor, correlation id and UTC time.
/// Persisted by the caller's <c>SaveChanges</c>, inside the same transaction as the business change.
/// </summary>
internal sealed class AuditTrail(
    LibraryDbContext dbContext,
    IActorAccessor actor,
    ICorrelationContext correlation,
    TimeProvider clock) : IAuditTrail
{
    public void Record(string entityType, Guid entityId, string action, object data)
    {
        var occurredAt = clock.GetUtcNow();

        dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(occurredAt),
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Actor = actor.ActorId,
            OccurredAt = occurredAt,
            CorrelationId = correlation.CorrelationId,
            Data = JsonSerializer.Serialize(data, JsonExtensions.StorageSerializerOptions)
        });
    }
}
